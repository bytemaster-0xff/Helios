-- ========================= HELIOS EXPORT ======================================
-- Copyright 2020 Helios Contributors
-- 
-- Helios is free software: you can redistribute it and/or modify
-- it under the terms of the GNU General Public License as published by
-- the Free Software Foundation, either version 3 of the License, or
-- (at your option) any later version.
-- 
-- Helios is distributed in the hope that it will be useful,
-- but WITHOUT ANY WARRANTY; without even the implied warranty of
-- MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
-- GNU General Public License for more details.
-- 
-- You should have received a copy of the GNU General Public License
-- along with this program.  If not, see <http://www.gnu.org/licenses/>.
--
-- This file is GENERATED by Helios Profile Editor.
-- You should not need to edit this file in most casees.

-- global scope for Helios public API, which is the code and state available to vehicle driver scripts
helios = {}

-- local scope for privileged interface used for testing
local helios_impl = {}

-- local scope for private code, to avoid name clashes
local helios_private = {}

-- report start up before configuration happens, in case of error in the configuration
log.write("HELIOS.EXPORT", log.INFO, "initializing Helios Export script")

-- ========================= CONFIGURATION ======================================
-- This section is configured by the DCS Interfaces in Helios Profile Editor

-- address to which we send
helios_private.host = "HELIOS_REPLACE_IPAddress"

-- UDP port to which we send
-- NOTE: our local port on which we listen is dynamic
helios_private.port = HELIOS_REPLACE_Port

-- how many export intervals have to pass before we send low priority data again
-- NOTE: this parameter has no configuration UI
helios_private.exportLowTickInterval = 2

-- seconds between updates (high priority export interval)
helios_impl.exportInterval = HELIOS_REPLACE_ExportInterval

-- seconds between updates for low priority data
-- NOTE: this parameter has no configuration UI
helios_impl.exportLowInterval = 0.2

-- maximum number of seconds without us sending anything
-- NOTE: Helios needs us to send something to discover our UDP client port number
-- NOTE: this parameter has no configuration UI
helios_impl.announceInterval = 3.0

-- seconds between announcements immediately after change in vehicle, to give
-- Helios a chance to discover us after it restarts its interface
-- NOTE: this parameter has no configuration UI
helios_impl.fastAnnounceInterval = 0.1

-- seconds after change in vehicle to use fast announcements
-- NOTE: this parameter has no configuration UI
helios_impl.fastAnnounceDuration = 1.0

-- minimum seconds between alert messages
-- if additional errors occur within this interval after an alert has been sent,
-- then the error is logged but no alert is sent
helios_impl.alertInterval = 5.0

-- Module names are different from internal self names, so this table translates them
-- without instantiating every module.  Planes must be entered into this table to be
-- able to use modules from the Scripts\Mods directory.
-- REVISIT: replace this mechanism with test loading and vehicle arrays
local helios_module_names = {
    ["A-10C"] = "Helios_A10C",
    ["A-10C_2"] = "Helios_A10C",
    ["F-14B"] = "Helios_F14",
    ["F-14A-135-GR"] = "Helios_F14",
    ["F-16C_50"] = "Helios_F16C",
    ["FA-18C_hornet"] = "Helios_F18C",
    ["A-10A"] = "Helios_FC",
    ["F-15C"] = "Helios_FC",
    ["MiG-29"] = "Helios_FC",
    ["Su-25"] = "Helios_FC",
    ["Su-27"] = "Helios_FC",
    ["Su-33"] = "Helios_FC",
    ["AV8BNA"] = "Helios_Harrier",
    ["UH-1H"] = "Helios_Huey",
    ["Ka-50"] = "Helios_KA50",
    -- legacy entry
    ["L-39"] = "Helios_L39",
    -- valid aircraft for L39 module
    ["L-39C"] = "Helios_L39",
    ["L-39ZA"] = "Helios_L39",
    ["Mi-8MT"] = "Helios_MI8",
    ["MiG-21Bis"] = "Helios_Mig21Bis",
    ["P-51D"] = "Helios_P51",
    ["P-51D-30-NA"] = "Helios_P51",
    ["TF-51D"] = "Helios_P51",
    ["SA342"] = "Helios_SA342",
    ["SA342L"] = "Helios_SA342",
    ["SA342M"] = "Helios_SA342",
    ["SA342Mistral"] = "Helios_SA342",
    ["SA342Minigun"] = "Helios_SA342",
    ["JF-17"] = "Helios_JF17"
}

-- ========================= HOOKS CALLED BY DCS =================================
-- DCS Export Functions call these indirectly

function helios_impl.LuaExportStart()
    -- called once just before mission start.
    package.path = package.path .. ";.\\LuaSocket\\?.lua"
    package.cpath = package.cpath .. ";.\\LuaSocket\\?.dll"

    helios_impl.init()
end

function helios_impl.LuaExportBeforeNextFrame()
    helios_private.processInput()

    helios_private.clock = LoGetModelTime()

    local updateHigh = false
    local updateLow = false
    if helios_private.clock >= helios_private.state.nextHighUpdate then
        updateHigh = true
        helios_private.state.nextHighUpdate = helios_private.calculateNextUpdate(helios_private.state.nextHighUpdate, helios_impl.exportInterval)
    end
    if helios_private.clock >= helios_private.state.nextLowUpdate then
        updateLow = true
        helios_private.state.nextLowUpdate = helios_private.calculateNextUpdate(helios_private.state.nextLowUpdate, helios_impl.exportLowInterval)        
    end

    if (not updateHigh) and (not updateLow) then
        -- not time yet
        return
    end

    -- check if vehicle type has changed
    local selfName = helios.selfName()
    if selfName ~= helios_private.previousSelfName then
        helios_private.handleSelfNameChange(selfName)
    end

    if helios_private.driver.processExports ~= nil then
        -- let driver do it
        local selfData = LoGetSelfData()
        helios_private.driver.processExports(selfData)
        if helios_private.driver.processSimulatorData ~= nil then
            helios_private.driver.processSimulatorData(selfData)
        end
    else
        local mainPanelDevice = GetDevice(0)
        if type(mainPanelDevice) == "table" then
            mainPanelDevice:update_arguments()
    
            if updateHigh then
                helios_private.processArguments(mainPanelDevice, helios_private.driver.everyFrameArguments)
                helios_private.driver.processHighImportance(mainPanelDevice)
                if helios_private.driver.processSimulatorData ~= nil then
                    helios_private.driver.processSimulatorData(LoGetSelfData())
                end
            end

            if updateLow then
                helios_private.processArguments(mainPanelDevice, helios_private.driver.arguments)
                helios_private.driver.processLowImportance(mainPanelDevice)
            end
        end    end

    local heartBeat = nil
    if helios_private.clock > (helios_impl.announceInterval + helios_private.state.lastSend) then
        -- if we sent nothing for a long time, send something just to let Helios discover us
        heartBeat = helios_impl.announceInterval
    end
    if helios_private.state.fastAnnounceTicks > 0 then
        -- immediately after changing vehicle or otherwise resetting, announce very fast
        helios_private.state.fastAnnounceTicks = helios_private.state.fastAnnounceTicks - 1
        if helios_private.clock > (helios_impl.fastAnnounceInterval + helios_private.state.lastSend) then
            heartBeat = helios_impl.fastAnnounceInterval
        end
    end

    if heartBeat ~= nil then
        log.write("HELIOS.EXPORT", log.DEBUG, string.format("sending alive announcement after %f seconds without any data sent (clock %f, sent %f)",
            heartBeat,
            helios_private.clock,
            helios_private.state.lastSend
        ))
        helios_private.doSend("ALIVE", "")
    end

    helios_private.flush();
end

function helios_impl.LuaExportAfterNextFrame()
end

function helios_impl.LuaExportStop()
    -- called once just after mission stop.
    helios_impl.unload()
end

-- ========================= PUBLIC API FOR DRIVERS --------======================
-- These are the functions that may be used in Scripts/Helios/Drivers/*.lua files
-- to implement support for a specific vehicle.

function helios.splitString(str, delim, maxNb)
    -- quickly handle edge case
    if string.find(str, delim) == nil then
        return { str }
    end

    -- optional limit on number of fields
    if maxNb == nil or maxNb < 1 then
        maxNb = 0 -- No limit
    end
    local result = {}
    local pat = "(.-)" .. delim .. "()"
    local nb = 0
    local lastPos
    for part, pos in string.gfind(str, pat) do
        nb = nb + 1
        result[nb] = part
        lastPos = pos
        if nb == maxNb then
            break
        end
    end
    -- Handle the last field
    if nb ~= maxNb then
        result[nb + 1] = string.sub(str, lastPos)
    end
    return result
end

function helios.round(num, idp)
    local mult = 10 ^ (idp or 0)
    return math.floor(num * mult + 0.5) / mult
end

function helios.ensureString(s)
    if type(s) == "string" then
        return s
    else
        return ""
    end
end

function helios.textureToString(s)
    if s == nil then
        return "0"
    else
        return "1"
    end
end

function helios.parseIndication(indicator_id)
    -- Thanks to [FSF]Ian code
    local ret = {}
    local li = list_indication(indicator_id)
    if li == "" then
        return nil
    end
    local m = li:gmatch("-----------------------------------------\n([^\n]+)\n([^\n]*)\n")
    while true do
        local name, value = m()
        if not name then
            break
        end
        ret[name] = value
    end
    return ret
end

-- send a value if its value has changed, batching sends
--
-- "format" is an optional format string to apply to the value 
-- (the formatted value is checked to see if it changed, not the raw one)
--
function helios.send(id, value, format)
    if value == nil or id == nil then
        return
    end
    if string.len(value) > 3 and value == string.sub("-0.00000000", 1, string.len(value)) then
        value = value:sub(2)
    end
    if format ~= nil then
        -- the FC2 functions for example expect formatting to be done by the send function
        value = string.format(format, value)
    end
    if helios_private.state.lastData[id] == nil or helios_private.state.lastData[id] ~= value then
        helios_private.doSend(id, value)
        helios_private.state.lastData[id] = value
    end
end
-- currently active vehicle/airplane
function helios.selfName()
    local info = LoGetSelfData()
    if info == nil then
        return ""
    end
    return info.Name
end

-- ========================= TESTABLE IMPLEMENTATION =============================
-- These functions are exported for use in mock testing and other tools, but are not 
-- for use by drivers or modules.  Keeping their interface stable allows the mock 
-- tester to continue to work.

-- called either from LuaExportStart hook or from hot reload
function helios_impl.init()
    log.write("HELIOS.EXPORT", log.DEBUG, "loading")

    -- load lusocket libraries
    log.write("HELIOS.EXPORT", log.DEBUG, "loading luasocket socket library")
    helios_private.socketLibrary = require("socket")
    log.write("HELIOS.EXPORT", log.DEBUG, "loading luasocket mime library")
    helios_private.mimeLibrary = require("mime")

    -- Simulation id
    log.write("HELIOS.EXPORT", log.DEBUG, "setting simulation ID")
    helios_private.simID = string.format("%08x*", os.time())

    -- most recently detected selfName
    helios_private.previousSelfName = ""

    -- event time 'now' as told to us by DCS
    helios_private.clock = 0

    -- init with empty driver that exports nothing by default (not even simulator data)
    -- NOTE: also clears state
    log.write("HELIOS.EXPORT", log.DEBUG, "installing empty driver")
    helios_impl.installDriver(helios_private.createDriver(), "", "")

    -- start service
    helios_private.clientSocket = helios_private.socketLibrary.udp()
    helios_private.clientSocket:setsockname("*", 0)
    helios_private.clientSocket:setoption("broadcast", true)
    helios_private.clientSocket:settimeout(0) -- non-blocking

    -- NOTE: the following code was in Helios from 2014 to 2020 for reasons nobody remembers:
    -- helios_private.clientSocket:settimeout(.001) -- blocking, but for a very short time

    log.write("HELIOS.EXPORT", log.DEBUG, "loaded")

    log.write(
        "HELIOS.EXPORT",
        log.INFO,
        string.format("Helios Export Script will send to %s on port %d", helios_private.host, helios_private.port)
	)
end

function helios_impl.unload()
    -- flush pending data, send DISCONNECT message so we can fire the Helios Disconnect event
    helios_private.doSend("DISCONNECT", "")
    helios_private.flush()

    -- free file descriptor and release port
    helios_private.clientSocket:close()

    log.write("HELIOS.EXPORT", log.DEBUG, "unloaded")
end

-- handle incoming message from Helios
function helios_impl.dispatchCommand(command)
    -- REVISIT: this is legacy code and does not guard anything
    local commandCode = string.sub(command, 1, 1)
    local rest = string.sub(command, 2):match("^(.-)%s*$");
    if (commandCode == "D") then
        local driverType = rest
        log.write("HELIOS.EXPORT", log.INFO, string.format("export driver of type '%s' requested by Helios", driverType))
        local selfName = helios.selfName()
        if driverType == 'HeliosDriver16' then
            selfName = helios_impl.loadDriver(driverType)
        elseif driverType == 'TelemetryOnly' then
            selfName = helios_impl.loadTelemetryDriver()
        elseif driverType == 'CaptZeenModule1' then
            selfName = helios_impl.loadModule(driverType)
        end
        helios_impl.notifySelfName(selfName)
        helios_impl.notifyLoaded()
    elseif helios_private.driver.processInput ~= nil then
        -- delegate commands other than 'P'
        helios_private.driver.processInput(command)
        helios_private.state.receiveCount = helios_private.state.receiveCount + 1
    elseif commandCode == "R" then
        -- reset command from Helios requests that we consider all values dirty
        helios_private.resetCachedValues()
    elseif (commandCode == "C") then
        -- click command from Helios
        local commandArgs = helios.splitString(rest, ",")
        local targetDevice = GetDevice(commandArgs[1])
        if type(targetDevice) == "table" then
            targetDevice:performClickableAction(commandArgs[2], commandArgs[3])
        end
        helios_private.state.receiveCount = helios_private.state.receiveCount + 1
    end
end

-- default simulator/common data export function to be used by all drivers unless
-- replaced via driver.processSimulatorData in the driver code
--
-- NOTE: not installed in CZ modules
function helios_private.processSimulatorData(selfData)
	local altBar = LoGetAltitudeAboveSeaLevel()
	local altRad = LoGetAltitudeAboveGroundLevel()
	local pitch, bank, yaw = LoGetADIPitchBankYaw()
	local vvi = LoGetVerticalVelocity()
	local ias = LoGetIndicatedAirSpeed()
	local aoa = LoGetAngleOfAttack()
	
	local glide = LoGetGlideDeviation()
	local side = LoGetSideDeviation()

	-- send this best effort (NOTE: helios.send will filter nil values)
	if (pitch ~= nil and bank ~= nil and yaw ~= nil) then
        -- these are text keys, which is very slightly slower but avoids collision with modules and drivers
		helios.send("T1", math.floor(0.5 + pitch * 57.3), "%d")
		helios.send("T2", math.floor(0.5 + bank * 57.3), "%d")
		helios.send("T3", math.floor(0.5 + yaw * 57.3), "%d")
		helios.send("T4", altBar, "%.1f")
		helios.send("T5", altRad, "%.1f")
        if math.abs(vvi) < 0.04 then
            -- don't send +/- 0.0 while bouncing on the ground
            vvi = 0.0
        end
		helios.send("T13", vvi, "%.1f")
		helios.send("T14", ias, "%.1f")
		helios.send("T16", aoa, "%.2f")
		helios.send("T17", glide, "%.3f")
		helios.send("T18", side, "%.3f")
		helios.send("T19", LoGetMachNumber(), "%.2f")
		local accel = LoGetAccelerationUnits()
		if accel ~= nil then
			helios.send("T20", accel.y or 0.0, "%.1f")
		end
	end
end

-- load an export driver that only provides the basic telemetry data included
-- in all drivers
function helios_impl.loadTelemetryDriver()
    local driver = helios_private.createDriver()
    driver.processSimulatorData = helios_private.processSimulatorData
    local currentSelfName = helios.selfName()
    helios_impl.installDriver(driver, "TelemetryOnly", currentSelfName)
    return currentSelfName
end

-- load an export driver of the given type for the current vehicle
-- the driverType is currently always HeliosDriver16
function helios_impl.loadDriver(driverType)
    if driverType == nil then
        error("missing driver type in request to load driver; program error")
    end

    -- create default driver which will be used as-is if we don't find any driver
    -- or otherwise will be merged with the contents of the driver file
    local driver = helios_private.createDriver()
    driver.processSimulatorData = helios_private.processSimulatorData

    local newDriverType = "TelemetryOnly"
    local success, result

    -- check if request is allowed
    local currentSelfName = helios.selfName()
    log.write("HELIOS.EXPORT", log.DEBUG, string.format("attempt to load driver for '%s'", currentSelfName))
    if helios_impl.driverType == "HeliosDriver16" and helios_impl.vehicle == currentSelfName then
        -- do nothing
        log.write("HELIOS.EXPORT", log.INFO, string.format("driver '%s' for '%s' is already loaded", helios_impl.driverType, currentSelfName))
        return currentSelfName
    elseif currentSelfName == "" then
        -- no vehicle, use default driver
        helios_impl.installDriver(driver, newDriverType, currentSelfName)
        return currentSelfName
    end

    -- now try to load specific driver
    local driverPath = string.format("%sScripts\\Helios\\Drivers\\%s.lua", lfs.writedir(), currentSelfName)
    local defaulted = false

    if (lfs.attributes(driverPath) ~= nil) then
        -- try to load
        success, result = pcall(dofile, driverPath)
    else
        local defaultDriverPath = string.format("%sScripts\\Helios\\Drivers\\Default.lua", lfs.writedir())
        if (lfs.attributes(defaultDriverPath) ~= nil) then
            defaulted = true
            success, result = pcall(dofile, defaultDriverPath)
        else
            -- normal case of driver not existing; no alert
            log.write("HELIOS.EXPORT", log.INFO, string.format("no driver for '%s' found", currentSelfName))
            helios_impl.installDriver(driver, newDriverType, currentSelfName)
            return currentSelfName                
        end
    end

    -- check result for nil, since driver may not have returned anything
    if success and result == nil then
        success = false
        result = string.format("driver %s did not return a driver object; incompatible with this export script",
            driverPath
        )
    end

    -- sanity check, make sure driver is for correct selfName, since race condition is possible
    if success and result.selfName ~= currentSelfName and not defaulted then
        success = false
        result = string.format("driver %s is for incorrect vehicle '%s'",
            driverPath,
            result.selfName
        )
    end

    if success then
        -- merge, replacing anything specified by the loaded file
        for k, v in pairs(result) do
            driver[k] = v
        end
        log.write("HELIOS.EXPORT", log.INFO, string.format("loaded driver '%s' for '%s'", driverType, driver.selfName))
        newDriverType = driverType
    else
        -- if the load fails, just leave the driver initialized to defaults
        log.write(
            "HELIOS.EXPORT",
            log.WARNING,
            string.format("failed to load driver '%s' for '%s'; disabling interface", driverType, currentSelfName)
        )
        log.write("HELIOS.EXPORT", log.WARNING, result)
        helios_private.sendAlert(result)
    end

    -- actually install the driver
    helios_impl.installDriver(driver, newDriverType, currentSelfName)
    return currentSelfName
end

-- load the module for the current vehicle, even if we previously loaded a driver
-- the driverType is currently always CaptZeenModule1
function helios_impl.loadModule(driverType)
    if driverType == nil then
        error("missing driver type in request to load module; program error")
    end
    local currentSelfName = helios.selfName()
    log.write("HELIOS.EXPORT", log.DEBUG, string.format("attempt to load module for '%s'", currentSelfName))
    local moduleName = helios_module_names[currentSelfName]
    if moduleName == nil then
        log.write("HELIOS.EXPORT", log.DEBUG, string.format("no matching module name for '%s' in table of modules known to export script", currentSelfName))
        return currentSelfName
    end
    if helios_impl.driverType == driverType then
        log.write("HELIOS.EXPORT", log.DEBUG, string.format("module '%s' already active for '%s'", moduleName, currentSelfName))
        return currentSelfName
    end
    local modulePath = string.format("%sScripts\\Helios\\Mods\\%s.lua", lfs.writedir(), moduleName)
    if (lfs.attributes(modulePath) ~= nil) then
        -- create wrapper around module to give it a driver interface
        local driver = helios_impl.createModuleDriver(currentSelfName, moduleName)
        if driver ~= nil then
            log.write("HELIOS.EXPORT", log.DEBUG, string.format("loaded module for '%s' from '%s'", currentSelfName, modulePath))
            helios_impl.installDriver(driver, driverType, currentSelfName)
        end
        -- if we fail, we just leave the previous driver installed
    else
        log.write("HELIOS.EXPORT", log.DEBUG, string.format("no module file for '%s' at '%s'", currentSelfName, modulePath))
    end
    return currentSelfName
end

function helios_impl.installDriver(driver, driverType, vehicle)
    -- shut down any existing driver
    if helios_private.driver ~= nil then
        helios_private.driver.unload()
        helios_private.driver = nil
    end

    -- install driver
    driver.init()
    helios_private.driver = driver
    helios_impl.driverType = driverType
    helios_impl.moduleName = driver.moduleName
    helios_impl.vehicle = vehicle

    -- drop any remmaining data and mark all values as dirty
    helios_private.clearState()
end

function helios_impl.notifyLoaded()
    log.write("HELIOS.EXPORT", log.INFO, string.format("notifying Helios of active driver '%s'", helios_impl.driverType))

    -- export code for 'currently active driver, reserved across all DCS interfacess
    helios_private.doSend("ACTIVE_DRIVER", helios_impl.driverType)
    helios_private.flush()
end

-- for testing
function helios_impl.setSimID(value)
    helios_private.simID = value
end

-- ========================= PRIVATE CODE ========================================

-- luasocket
helios_private.socketLibrary = nil -- lazy init

function helios_private.clearState()
    if (helios_private.state ~= nil and helios_private.state.sendCount ~= nil) then
        log.write(
            "HELIOS.EXPORT", 
            log.INFO, 
            string.format("sent %d updates and received %d commands since last reporting", helios_private.state.sendCount, helios_private.state.receiveCount))
    end
    
    helios_private.state = {}

    helios_private.state.packetSize = 0
    helios_private.state.sendStrings = {}
    helios_private.state.lastData = {}

    -- event time of last message sent
    helios_private.state.lastSend = 0

    -- future event time when we are allowed to send an alert if an error occurs
    helios_private.state.nextAlert = 0

    -- next high and low priority updates
    helios_private.state.nextHighUpdate = 0
    helios_private.state.nextLowUpdate = 0

    -- ticks of fast announcement remaining
    helios_private.state.fastAnnounceTicks = helios_impl.fastAnnounceDuration / helios_impl.exportInterval

    -- sent updates and received commands, just for reporting
    helios_private.state.sendCount = 0
    helios_private.state.receiveCount = 0
end

function helios_private.processArguments(device, arguments)
    if arguments == nil then
        return
    end
    local lArgumentValue
    for lArgument, lFormat in pairs(arguments) do
        success, lArgumentValue = pcall(string.format, lFormat, device:get_argument_value(lArgument))
        if not success then
            log.write("HELIOS.EXPORT", log.ERROR, string.format("argument %d has an invalid format string '%s'", lArgument, lFormat))
            error(lArgumentValue)
        end
        helios.send(lArgument, lArgumentValue)
    end
end

-- sends without checking if the value has changed
function helios_private.doSend(id, value)
    local data = id .. "=" .. value
    local dataLen = string.len(data)

    if dataLen + helios_private.state.packetSize > 576 then
        helios_private.flush()
    end

    table.insert(helios_private.state.sendStrings, data)
    helios_private.state.packetSize = helios_private.state.packetSize + dataLen + 1
end

function helios_private.flush()
    local toSend = #helios_private.state.sendStrings
    if toSend > 0 then
        helios_private.state.sendCount = helios_private.state.sendCount + toSend
        local packet = helios_private.simID .. table.concat(helios_private.state.sendStrings, ":") .. "\n"
        helios_private.socketLibrary.try(helios_private.clientSocket:sendto(packet, helios_private.host, helios_private.port))
        helios_private.state.lastSend = helios_private.clock
        helios_private.state.sendStrings = {}
        helios_private.state.packetSize = 0
    end
end

function helios_private.resetCachedValues()
    helios_private.state.lastData = {}

    -- make sure low priority is sent also
    helios_private.state.nextLowUpdate = helios_private.state.nextHighUpdate
end

function helios_private.processInput()
    local success, lInput = pcall(helios_private.clientSocket.receive, helios_private.clientSocket)
    if not success then
        -- happens on interrupt
        return
    end
    if lInput then
        helios_impl.dispatchCommand(lInput)
    end
end

function helios_private.createDriver()
    -- defaults
    local driver = {}
    driver.selfName = ""
    driver.everyFrameArguments = {}
    driver.arguments = {}
    function driver.processHighImportance()
        -- do nothing
    end
    function driver.processLowImportance()
        -- do nothing
    end
    function driver.init()
        -- do nothing
    end
    function driver.unload()
        -- do nothing
    end
    return driver
end

function helios_impl.notifySelfName(selfName)
    if selfName == nil then
        error("uninitialized self name detected; program error")
    end
    -- export code for 'currently active vehicle, reserved across all DCS interfacess
    log.write("HELIOS.EXPORT", log.INFO, string.format("notifying Helios of active vehicle '%s'", selfName))
    helios_private.doSend("ACTIVE_VEHICLE", selfName)
    helios_private.flush()
end

function helios_private.handleSelfNameChange(selfName)
    log.write(
        "HELIOS.EXPORT",
        log.INFO,
        string.format("changed vehicle from '%s' to '%s'", helios_private.previousSelfName, selfName)
    )

    helios_private.previousSelfName = selfName
    helios_impl.moduleName = nil

    -- load module when present
    selfName = helios_impl.loadModule("CaptZeenModule1");
    if (helios_impl.moduleName == nil) then
        -- load driver or install fallback driver
        selfName = helios_impl.loadDriver("HeliosDriver16");
    end

    -- tell Helios results
    helios_impl.notifySelfName(selfName)
    helios_impl.notifyLoaded()
end

--- send a failure message to Helios, if allowed by rate control
function helios_private.sendAlert(message)
    if helios_private.clock >= helios_private.state.nextAlert then
        helios_private.state.nextAlert = helios_private.clock + helios_impl.alertInterval
        log.write('HELIOS EXPORT', log.DEBUG, string.format("sending error alert message at event time %f", helios_private.clock))
        helios_private.doSend("ALERT_MESSAGE", (helios_private.mimeLibrary.b64(message)):gsub("=","-"))
        helios_private.flush()
    end
end

--- calculate next update time
function helios_private.calculateNextUpdate(currentUpdate, updateInterval) 
    -- schedule next round relative to expiration, not relative to now
    local nextUpdate = currentUpdate + updateInterval

    -- check if already expired
    if nextUpdate < helios_private.clock then
        -- either newly initialized or we are rendering too slow
        -- and have falled behind, reset from now
        nextUpdate = helios_private.clock + updateInterval
    end

    return nextUpdate
end

-- ========================= MODULE COMPATIBILITY LAYER ==========================
-- These functions make this script compatible with Capt Zeen Helios modules.
-- Simply place the modules in the Scripts/Helios/Mods folder and make sure they
-- referenced in the table helios_module_names near the top of this script.

-- when a module is running, this will be global scope Helios_Udp
local helios_modules_udp = {
}

-- when a module is running, this will be global scope Helios_Util
local helios_modules_util = {
}

-- creates a wrapper around a Capt Zeen Module to make it act as a Helios Driver
function helios_impl.createModuleDriver(selfName, moduleName)
    local driver = helios_private.createDriver()
    driver.moduleName = moduleName

    function driver.init()
        -- prepare environment
        Helios_Udp = helios_modules_udp;
        Helios_Util = helios_modules_util;
    end

    function driver.unload()
        Helios_Udp = nil
        Helios_Util = nil
        _G[moduleName] = nil -- luacheck: no global
    end

    -- execute module
    local modulePath = string.format("%sScripts\\Helios\\Mods\\%s.lua", lfs.writedir(), moduleName)
    local success, result = pcall(dofile, modulePath)
    if success then
        result = _G[moduleName] -- luacheck: no global
    end

    -- check result for nil, since module may not have returned anything
    if success and result == nil then
        success = false
        result = string.format("module %s did not create module object %s; incompatible with this export script",
            modulePath, moduleName
        )
    end

    -- sanity check, make sure module is for correct selfName
    if success then
        local names = helios.splitString(result.Name, ";")
        local supported = false
        for _, name in pairs(names) do
            if name == selfName then
                supported = true
            end
        end
        if not supported then
            success = false
            result = string.format("module %s does not support '%s', only %s",
                moduleName,
                selfName,
                table.concat(names, ", ")
            )
        end
    end

    if not success then
        log.write('HELIOS EXPORT', log.INFO, string.format("could not create module driver '%s' for '%s'", moduleName, selfName))
        log.write('HELIOS EXPORT', log.INFO, result)
        helios_private.sendAlert(string.format("failed to load module '%s': %s", moduleName, result))
        return nil
    end

    -- hook it up
    driver.selfName = selfName
    driver.everyFrameArguments = result.HighImportanceArguments
    driver.arguments = result.LowImportanceArguments
    if result.HighImportance ~= nil then
        driver.processHighImportance = result.HighImportance
    end
    if result.LowImportance ~= nil then
        driver.processLowImportance = result.LowImportance
    end
    if result.ProcessInput ~= nil then
        driver.processInput = result.ProcessInput
    end
    if result.FlamingCliffsAircraft then
        -- override all export processing, even if no hook provided
        if result.ProcessExports ~= nil then
            driver.processExports = result.ProcessExports
        else
            function driver.processExports()
                -- do nothing
            end
        end
    end
    return driver
end

-- REVISIT: this will all be replaced by a sandbox
helios_modules_udp.Send = helios.send -- same signature
helios_modules_udp.Flush = helios_private.flush -- same signature
helios_modules_udp.ResetChangeValues = helios_private.resetCachedValues -- same signature

function helios_modules_util.Split(text, pattern)
    local ret = {}
    local findpattern = "(.-)"..pattern
    local last = 1
    local startpos, endpos, str = text:find(findpattern, 1)

    while startpos do
       if startpos ~= 1 or str ~= "" then table.insert(ret, str) end
       last = endpos + 1
       startpos, endpos, str = text:find(findpattern, last)
    end

    if last <= #text then
       str = text:sub(last)
       table.insert(ret, str)
    end

    return ret
end

function helios_modules_util.Degrees(radians)
    if radians == nil then
        return 0.0
    end
	return radians * 57.2957795
end

function helios_modules_util.Convert_Lamp(valor_lamp)
	return (valor_lamp  > 0.1) and 1 or 0
end

function helios_modules_util.Convert_SW (valor)
	return math.abs(valor-1)+1
end

function helios_modules_util.ValueConvert(actual_value, input, output)
	local range=1
	local slope = {}

	for a=1,#output-1 do -- calculating the table of slopes
		slope[a]= (input[a+1]-input[a]) / (output[a+1]-output[a])
	end

	for a=1,#output-1 do
		if actual_value >= output[a] and actual_value <= output[a+1] then
			range = a
			break
		end     -- check the range of the value
	end

	local final_value = ( slope[range] * (actual_value-output[range]) ) + input[range]
	return final_value
end

helios_modules_util.GetListIndicator = helios.parseIndication -- same signature

-- ========================= CONNECTION TO DCS ===================================
-- these are the functions we actually export, and third party scripts may retain
local arg1 = ...
if arg1 == "nohooks" then
    -- running under loader, don't place any hooks
    return helios_impl
end
log.write("HELIOS.EXPORT", log.INFO, "Helios registering DCS Lua callbacks")

-- save and chain any previous exports
helios_private.previousHooks = {}
helios_private.previousHooks.LuaExportStart = LuaExportStart
helios_private.previousHooks.LuaExportStop = LuaExportStop
helios_private.previousHooks.LuaExportBeforeNextFrame = LuaExportBeforeNextFrame
helios_private.previousHooks.LuaExportAfterNextFrame = LuaExportAfterNextFrame

-- utility to chain one DCS hook without arguments
function helios_private.chainHook(functionName)
    _G[functionName] = function() -- luacheck: no global
        -- try execute Helios version of hook
        local success, result = pcall(helios_impl[functionName])
        if not success then
            log.write("HELIOS.EXPORT", log.ERROR, string.format("error return from Helios implementation of '%s'", functionName))
            if type(result) == "string" then
                log.write("HELIOS.EXPORT", log.ERROR, result)
                helios_private.sendAlert(functionName..result)
            end
        end
        -- chain to next export script, if any
        local nextHandler = helios_private.previousHooks[functionName]
        if nextHandler ~= nil then
            success, result = pcall(nextHandler)
            if not success then
                log.write("HELIOS.EXPORT", log.ERROR, string.format("error return from chained third-party implementation of '%s'", functionName))
                if type(result) == "string" then
                    log.write("HELIOS.EXPORT", log.ERROR, result)
                    helios_private.sendAlert("thirdparty "..functionName..result)
                end
            end
        end
    end
end

-- hook all the basic functions without arguments
helios_private.chainHook("LuaExportStart")
helios_private.chainHook("LuaExportStop")
helios_private.chainHook("LuaExportAfterNextFrame")
helios_private.chainHook("LuaExportBeforeNextFrame")

-- when running under one of our tools, these functions are accessible to our wrapper
return helios_impl
