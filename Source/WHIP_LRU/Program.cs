using System;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using Chattel;
using log4net;
using log4net.Config;
using Nini.Config;
using WHIP_LRU.Server;
using WHIP_LRU.Util;

namespace WHIP_LRU {
	class Application {
		private static readonly ILog LOG = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		private static readonly string EXECUTABLE_DIRECTORY = Path.GetDirectoryName(Assembly.GetEntryAssembly().CodeBase.Replace("file:/", string.Empty));

		private static readonly string DEFAULT_INI_FILE = "WHIP_LRU.ini";

		private static readonly string COMPILED_BY = "?mono?"; // Replaced during automatic packaging.

		private static IConfigSource _configSource;

		private static ChattelReader _assetReader;
		private static ChattelWriter _assetWriter;

		private static bool _isRunning = true;

		public static int Main(string[] args) {
			// First line, hook the appdomain to the crash reporter
			AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

			// Add the arguments supplied when running the application to the configuration
			var configSource = new ArgvConfigSource(args);
			_configSource = configSource;

			// Commandline switches
			configSource.AddSwitch("Startup", "inifile");
			configSource.AddSwitch("Startup", "logconfig");

			var startupConfig = _configSource.Configs["Startup"];

			var pidFile = new PIDFileManager(startupConfig.GetString("pidfile", string.Empty));

			// Configure Log4Net
			var logConfigFile = startupConfig.GetString("logconfig", string.Empty);
			if (string.IsNullOrEmpty(logConfigFile)) {
				XmlConfigurator.Configure();
				LogBootMessage();
				LOG.Info("[MAIN] Configured log4net using ./WHIP_LRU.exe.config as the default.");
			}
			else {
				XmlConfigurator.Configure(new FileInfo(logConfigFile));
				LogBootMessage();
				LOG.Info($"[MAIN] Configured log4net using \"{logConfigFile}\" as configuration file.");
			}

			// Configure nIni aliases and locale
			Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US", true);

			configSource.Alias.AddAlias("On", true);
			configSource.Alias.AddAlias("Off", false);
			configSource.Alias.AddAlias("True", true);
			configSource.Alias.AddAlias("False", false);
			configSource.Alias.AddAlias("Yes", true);
			configSource.Alias.AddAlias("No", false);

			// Read in the ini file
			ReadConfigurationFromINI(configSource);

			var chattelConfigRead = new ChattelConfiguration(configSource, configSource.Configs["AssetsRead"]);
			chattelConfigRead.DisableCache(); // Force caching off no matter how the INI is set. Doing this differently here.
			_assetReader = new ChattelReader(chattelConfigRead);
			var chattelConfigWrite = new ChattelConfiguration(configSource, configSource.Configs["AssetsWrite"]);
			chattelConfigWrite.DisableCache(); // Force caching off no matter how the INI is set. Doing this differently here.
			_assetWriter = new ChattelWriter(chattelConfigWrite);

			pidFile.SetStatus(PIDFileManager.Status.Starting);

			// Start up the service.
			using (var server = new WHIPServer(RequestReceivedDelegate)) {
				pidFile.SetStatus(PIDFileManager.Status.Running);

				// Handlers for signals.
				Console.CancelKeyPress += delegate {
					LOG.Debug("CTRL-C pressed, terminating.");
					_isRunning = false;
					server.Stop();
				};

				// Handle signals!
				while (_isRunning) {
					try {
						server.Start();
					}
					catch (SocketException e) {
						LOG.Error("Unable to bind to address or port. Is something already listening on it, or have you granted permissions for WHIP_LRU to listen?", e);
						_isRunning = false;
					}
					catch (Exception e) {
						LOG.Warn("Exception during server execution, automatically restarting.", e);
					}
				}
			}

			// I don't care what's still connected or keeping things running, it's time to die!
			Environment.Exit(0);
			return 0;
		}

		public static ServerResponseMsg RequestReceivedDelegate(ClientRequestMsg request) {




			return null;
		}

		private static void LogBootMessage() {
			LOG.Info("* * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * *");
			LOG.Info($"WHIP_LRU v{Assembly.GetExecutingAssembly().GetName().Version.ToString()} {COMPILED_BY}");
			var bitdepth = Environment.Is64BitOperatingSystem ? "64bit" : "unknown or 32bit";
			LOG.Info($"OS: {Environment.OSVersion.VersionString} {bitdepth}");
			LOG.Info($"Commandline: {Environment.CommandLine}");
			LOG.Info($"CWD: {Environment.CurrentDirectory}");
			LOG.Info($"Machine: {Environment.MachineName}");
			LOG.Info($"Processors: {Environment.ProcessorCount}");
			LOG.Info($"User: {Environment.UserDomainName}/{Environment.UserName}");
			var isMono = Type.GetType("Mono.Runtime") != null;
			LOG.Info("Interactive shell: " + (Environment.UserInteractive ? "yes" : isMono ? "indeterminate" : "no"));
			LOG.Info("* * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * *");
		}

		private static void ReadConfigurationFromINI(IConfigSource configSource) {
			var startupConfig = configSource.Configs["Startup"];
			var iniFileName = startupConfig.GetString("inifile", DEFAULT_INI_FILE);

			var found_at_given_path = false;

			try {
				LOG.Info($"[MAIN] Attempting to read configuration file {Path.GetFullPath(iniFileName)}");
				startupConfig.ConfigSource.Merge(new IniConfigSource(iniFileName));
				LOG.Info($"[MAIN] Success reading configuration file.");
				found_at_given_path = true;
			}
			catch {
				LOG.Warn($"[MAIN] Failure reading configuration file at {Path.GetFullPath(iniFileName)}");
			}

			if (!found_at_given_path) {
				// Combine with true path to binary and try again.
				iniFileName = Path.Combine(EXECUTABLE_DIRECTORY, iniFileName);

				try {
					LOG.Info($"[MAIN] Attempting to read configuration file from installation path {Path.GetFullPath(iniFileName)}");
					startupConfig.ConfigSource.Merge(new IniConfigSource(iniFileName));
					LOG.Info($"[MAIN] Success reading configuration file.");
				}
				catch {
					LOG.Fatal($"[MAIN] Failure reading configuration file at {Path.GetFullPath(iniFileName)}");
					throw;
				}
			}
		}

		#region Crash handler

		private static bool _isHandlingException;

		/// <summary>
		/// Global exception handler -- all unhandled exceptions end up here :)
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e) {
			if (_isHandlingException) {
				return;
			}

			try {
				_isHandlingException = true;

				var msg = string.Empty;

				var ex = (Exception)e.ExceptionObject;
				if (ex.InnerException != null) {
					msg = $"InnerException: {ex.InnerException}\n";
				}

				msg = $"[MAIN] APPLICATION EXCEPTION DETECTED: {e}\n" +
					"\n" +
					$"Exception: {e.ExceptionObject}\n" +
					msg +
					$"\nApplication is terminating: {e.IsTerminating}\n";

				LOG.Fatal(msg);

				if (e.IsTerminating) {
					// Since we are crashing, there's no way that log4net.RollbarNET will be able to send the message to Rollbar directly.
					// So have a separate program go do that work while this one finishes dying.

					var raw_msg = System.Text.Encoding.Default.GetBytes(msg);

					var err_reporter = new System.Diagnostics.Process();
					err_reporter.EnableRaisingEvents = false;
					err_reporter.StartInfo.FileName = Path.Combine(EXECUTABLE_DIRECTORY, "RollbarCrashReporter.exe");
					err_reporter.StartInfo.WorkingDirectory = EXECUTABLE_DIRECTORY;
					err_reporter.StartInfo.Arguments = raw_msg.Length.ToString(); // Let it know ahead of time how many characters are expected.
					err_reporter.StartInfo.RedirectStandardInput = true;
					err_reporter.StartInfo.RedirectStandardOutput = false;
					err_reporter.StartInfo.RedirectStandardError = false;
					err_reporter.StartInfo.UseShellExecute = false;
					if (err_reporter.Start()) {
						err_reporter.StandardInput.BaseStream.Write(raw_msg, 0, raw_msg.Length);
					}
				}
			}
			catch (Exception ex) {
				LOG.Error("[MAIN] Exception launching CrashReporter.", ex);
			}
			finally {
				_isHandlingException = false;

				if (e.IsTerminating) {
					// Preempt to not show a pile of puke if console was disabled.
					Environment.Exit(1);
				}
			}
		}

		#endregion
	}
}
