using System;
using System.IO;
using System.Reflection;
using System.Threading;
using Chattel;
using LibWhipLru;
using log4net;
using log4net.Config;
using Mono.Unix;
using Mono.Unix.Native;
using Nini.Config;
using WHIP_LRU.Server;
using WHIP_LRU.Util;

namespace WHIP_LRU {
	class Application {
		private static readonly ILog LOG = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		private static readonly string EXECUTABLE_DIRECTORY = Path.GetDirectoryName(Assembly.GetEntryAssembly().CodeBase.Replace("file:/", string.Empty));

		private static readonly string DEFAULT_INI_FILE = Path.Combine(EXECUTABLE_DIRECTORY, "WHIP_LRU.ini");

		private static readonly string COMPILED_BY = "?mono?"; // Replaced during automatic packaging.

		public static int Main(string[] args) {
			// First line, hook the appdomain to the crash reporter
			AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

			// Add the arguments supplied when running the application to the configuration
			var configSource = new ArgvConfigSource(args);

			// Commandline switches
			configSource.AddSwitch("Startup", "inifile");
			configSource.AddSwitch("Startup", "logconfig");
			configSource.AddSwitch("Startup", "pidfile");

			var startupConfig = configSource.Configs["Startup"];

			var pidFileManager = new PIDFileManager(startupConfig.GetString("pidfile", string.Empty));

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

			var isRunning = true;
			WhipLru whipLru = null;

			// Handlers for signals.
			Console.CancelKeyPress += delegate {
				LOG.Debug("CTRL-C pressed, terminating.");
				isRunning = false;
				whipLru?.Stop();
			};

			var signals = new UnixSignal[]{
				new UnixSignal(Signum.SIGINT),
				new UnixSignal(Signum.SIGTERM),
				new UnixSignal(Signum.SIGHUP),
			};

			while (isRunning) {
				// Read in the ini file
				ReadConfigurationFromINI(configSource);

				var chattelConfigRead = new ChattelConfiguration(configSource, configSource.Configs["AssetsRead"]);
				var chattelConfigWrite = new ChattelConfiguration(configSource, configSource.Configs["AssetsWrite"]);

				var serverConfig = configSource.Configs["Server"];

				var address = serverConfig.GetString("Address", WHIPServer.DEFAULT_ADDRESS);
				var port = (uint)serverConfig.GetInt("Port", (int)WHIPServer.DEFAULT_PORT);
				var password = serverConfig.GetString("Password", WHIPServer.DEFAULT_PASSWORD);

				whipLru = new WhipLru(address, port, password, pidFileManager, chattelConfigRead, chattelConfigWrite);

				whipLru.Start();

				var signalIndex = UnixSignal.WaitAny(signals, -1);

				switch (signals[signalIndex].Signum) {
					case Signum.SIGHUP:
						whipLru.Stop();
					break;
					case Signum.SIGINT:
					case Signum.SIGKILL:
						isRunning = false;
						whipLru.Stop();
					break;
				}
			}

			return 0;
		}

		#region Bootup utils

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

		#endregion

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
