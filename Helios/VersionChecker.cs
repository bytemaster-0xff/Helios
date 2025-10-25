//  Copyright 2014 Craig Courtney
//    
//  Helios is free software: you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//
//  Helios is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with this program.  If not, see <http://www.gnu.org/licenses/>.

namespace GadrocsWorkshop.Helios
{
    using GadrocsWorkshop.Helios.Util;
    using GadrocsWorkshop.Helios.Windows.Controls;
    using GadrocsWorkshop.Helios.Windows.ViewModel;
    using System;
    using System.IO;
    using System.Net;
    using System.Reflection;
    using System.Windows;

    public class VersionChecker
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// backing field for LastReturnedVersion
        /// the last version number we found online and offered to the user, persisted
        /// </summary>
        private Version _lastReturnedVersion;

        /// <summary>
        /// backing field for ReminderForNextRelease, contains
        /// next date (as string yyyyMMdd) when we will show reminders again, persisted
        /// </summary>
        private string _nextDateForVersionWarning = "";

        /// <summary>
        /// if true, this is a development prototype that does not use version checks and expires instead
        /// </summary>
        private readonly bool _developmentPrototype;

        /// <summary>
        /// datetime value when this build expires, currently set from a global timestamp in Helios
        /// </summary>
        private readonly DateTime _timeBombTime;

        // this is also used by known links utility
        internal const string GITHUB_DOWNLOAD_URL_SETTING = "LastestGitHubDownloadUrl";

        /// <summary>
        /// backing field for ReminderForNextRelease, contains
        /// true if we remind on next release, regardless of time, persisted
        /// </summary>
        private bool _reminderForNextRelease;

        private readonly ISettingsManager _settings;

        private readonly bool _settingsAreWritable;

        internal VersionChecker(ISettingsManager settings, bool settingsAreWritable)
        {
            _settings = settings;
            _settingsAreWritable = settingsAreWritable;
            RunningVersion = Helios.RunningVersion.FromHeliosAssembly();
            if (Helios.RunningVersion.IsDevelopmentPrototype)
            {
                _developmentPrototype = true;
                _timeBombTime = TimeStamp.CompilationTimestampUtc.AddDays(365.0);
            }
            _lastReturnedVersion = ToVersion(_settings.LoadSetting("Helios", "LastReturnedVersion", "0.0.0.0"));
            _nextDateForVersionWarning = _settings.LoadSetting("Helios", "NextDateForVersionWarning", TodayString);
            _reminderForNextRelease = _settings.LoadSetting("Helios", "ReminderForNextRelease", "0")=="1";
        }
        
        public Check CheckAvailableVersion(bool forceCheckNow)
        {
            if (_developmentPrototype && !forceCheckNow)
            {
                // we don't auto upgrade, we just time out
                CheckTimeBomb();
                return null;
            }

            if (RunningVersion == null)
            {
                // no data
                Logger.Info("upgrade checks will not work since the current assembly does not have version info");
                return null;
            }

            try
            {
                string today = TodayString;
                if (NextDateForVersionWarning == "")
                {
                    NextDateForVersionWarning = today;
                }

                // first see if it is time to make a network request
                if (!forceCheckNow && string.Compare(NextDateForVersionWarning, today, StringComparison.Ordinal) > 0)
                {
                    Logger.Debug("no network request made, because the next check is not due until {DateText}", NextDateForVersionWarning);
                    return null;
                }

                // go find the latest release
                Check versionCheck = new Check(RunningVersion);
                if (versionCheck.DownloadUrl != null && _settingsAreWritable)
                {
                    _settings.SaveSetting("Helios", GITHUB_DOWNLOAD_URL_SETTING, versionCheck.DownloadUrl);
                }

                return versionCheck;
            }
            catch (Exception e)
            {
                Logger.Error(e, "error during check for available versions");
                return null;
            }
        }

        /// <summary>
        /// interactively offer new versions, using and updating user preferences regarding notifications
        /// </summary>
        /// <param name="forceCheck"></param>
        /// <param name="forceDisplay"></param>
        public void CheckAvailableVersionsWithDialog(bool forceCheck, bool forceDisplay)
        {
            Check latest = CheckAvailableVersion(forceCheck);

            if (!forceDisplay && !ShouldOfferNewVersion(latest))
            {
                return;
            }

            Logger.Debug("offering available version");
            PresentVersionDialog(latest);
        }

        public bool ShouldOfferNewVersion(Check versionCheck)
        {
            if (versionCheck == null)
            {
                Logger.Debug("no fresh result from version check");
                return false;
            }

            if (!versionCheck.HasNewVersion)
            {
                Logger.Debug("no new version to offer");
                return false;
            }

            if (versionCheck.AvailableVersion.CompareTo(LastReturnedVersion) >= 0)
            {
                Logger.Debug("new version {Avaialable} is greater than most recently offered {Previous}",
                    versionCheck.AvailableVersion, LastReturnedVersion);
                return true;
            }

            if (ReminderForNextRelease)
            {
                Logger.Debug("user has declined new versions until next release");
                return false;
            }

            return true;
        }

        private void PresentVersionDialog(Check versionCheck)
        {
            // we will now offer this version, so it is the new reference for "wait until next release"
            LastReturnedVersion = versionCheck.AvailableVersion;

            // reset (to 7 day checks)
            ReminderForNextRelease = false;

            // present dialog
            VersionCheckWindow dialog = new VersionCheckWindow
            {
                DataContext = new VersionCheckViewModel(versionCheck)
            };
            dialog.ShowDialog();
        }

        internal void HandleDowloadStarted(string downloadUrl)
        {
            _settings.LoadSetting("Helios", GITHUB_DOWNLOAD_URL_SETTING, downloadUrl);

            // if the user has downloaded the new version, set a nag if they have not installed it
            RemindInDays(0);
        }

        private static string TodayString => DateTime.Today.ToString("yyyyMMdd");

        private void CheckTimeBomb()
        {
            if (_timeBombTime.CompareTo(DateTime.Now) < 0 && false)
            {
                string message =
                    $"this is a development prototype build that expired on {_timeBombTime.ToLongDateString()}";
                Logger.Error(message);
                MessageBox.Show(message, "Please get a new Development build of Helios");
                Application.Current.Shutdown();
            }

            Logger.Info($"this is a development prototype build that will expire on {_timeBombTime.ToLongDateString()}");
        }

        public Version RunningVersion { get; }

        public string NextDateForVersionWarning
        {
            get => _nextDateForVersionWarning;
            set
            {
                _nextDateForVersionWarning = value;
                if (_settingsAreWritable)
                {
                    _settings.SaveSetting("Helios", "NextDateForVersionWarning", _nextDateForVersionWarning);
                }
            }
        }

        /// <summary>
        /// utility to switch off reminder for next release and set timer for number of days from now
        /// </summary>
        /// <param name="days">relative number of days from now</param>
        internal void RemindInDays(int days)
        {
            NextDateForVersionWarning = DateTime.Now.AddDays(days).ToString("yyyyMMdd");
            ReminderForNextRelease = false;
        }

        public bool ReminderForNextRelease
        {
            get => _reminderForNextRelease;
            internal set
            {
                if (_reminderForNextRelease == value)
                {
                    return;
                }
                _reminderForNextRelease = value;
                if (_settingsAreWritable)
                {
                    _settings.SaveSetting("Helios", "ReminderForNextRelease", _reminderForNextRelease ? "1" : "0");
                }
                if (value)
                {
                    NextDateForVersionWarning = DateTime.Now.AddDays(7).ToString("yyyyMMdd");
                }
            }
        }

        /// <summary>
        /// the last version number we found online and offered to the user, persisted
        /// </summary>
        public Version LastReturnedVersion
        {
            get => _lastReturnedVersion;
            set
            {
                if (_lastReturnedVersion == value)
                {
                    return;
                }

                if (_settingsAreWritable)
                {
                    _settings.SaveSetting("Helios", "LastReturnedVersion", VersionToString(_lastReturnedVersion));
                }

                _lastReturnedVersion = value;
            }
        }

        internal static string VersionToString(Version version)
        {
            return $"{version.Major}.{version.Minor}.{version.Build:0000}.{version.Revision:0000}";
        }

        private static Version ToVersion(string versionString)
        {
            if (!Version.TryParse(versionString, out Version result))
            {
                throw new ArgumentOutOfRangeException(nameof(versionString), versionString, @"is not a valid version string");
            }
            return result;
        }

        /// <summary>
        /// a specific check for a new version and its result
        /// </summary>
        public class Check: NotificationObject
        {
            public Check(Version currentVersion)
            {
                CurrentVersion = currentVersion;
                AvailableVersion = CheckGithubVersion();
            }

            public Version CurrentVersion
            {
                get;
            }

            public Version AvailableVersion         
            {
                get;

                // settable only for benefit of design data
                internal set;
            }

            public string DownloadUrl
            {
                get;
                private set;
            }

            public bool HasNewVersion => CurrentVersion.CompareTo(AvailableVersion) < 0;

            public string NotesUrl
            {
                get; 
                private set;
            }

            /// <summary>
            /// check for a new version on github
            /// </summary>
            private Version CheckGithubVersion()
            {
                try
                {
                    // check github latest release URL
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
                    ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
                    string repo = KnownLinks.GitRepoUrl();
                    if (repo == null)
                    {
                        Logger.Error($"cannot perform upgrade check because we don't know have a valid git repo URL to check; please check the '{GITHUB_DOWNLOAD_URL_SETTING}' and 'Repo' variables in HeliosSettings.xml");
                        return CurrentVersion;
                    }
                    HttpWebRequest wreq = (HttpWebRequest)WebRequest.Create($"{repo}releases/latest");
                    wreq.MaximumAutomaticRedirections = 4;
                    wreq.MaximumResponseHeadersLength = 1000;
                    wreq.Credentials = CredentialCache.DefaultCredentials;
                    HttpWebResponse wrsp = (HttpWebResponse)wreq.GetResponse();
                    try
                    {
                        if (wrsp.StatusCode == HttpStatusCode.OK)
                        {
#if HELIOS_32BIT
                            DownloadUrl = $"{wrsp.ResponseUri.AbsoluteUri.Replace("/tag/", "/download/")}/Helios32Bit_Installers.zip";
#else
                            DownloadUrl = $"{wrsp.ResponseUri.AbsoluteUri.Replace("/tag/", "/download/")}/Helios_Installers.zip";
#endif
                            NotesUrl = wrsp.ResponseUri.AbsoluteUri;
                            return ToVersion(Path.GetFileName(wrsp.ResponseUri.LocalPath));
                        }
                        else
                        {
                            Logger.Error("cannot perform upgrade check because access to {URL} resulted in unexpected result code {Code}",
                                wreq.RequestUri, wrsp.StatusCode);
                            return CurrentVersion;
                        }
                    }
                    finally
                    {
                        wrsp.Close();
                    }

                }
                catch (Exception e)
                {
                    Logger.Error(e, "error retrieving available version from github.");
                    return CurrentVersion;
                }
            }
        }
    }
}
