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
using LibWhipLru.Server;
using LibWhipLru.Util;
using LibWhipLru.Cache;
using System.Linq;
using System.Collections.Generic;
using System.Net.Sockets;

namespace WHIP_LRU {
	static class Application {
		private static readonly ILog LOG = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		private static readonly bool ON_POSIX_COMPLAINT_OS = Type.GetType("Mono.Runtime") != null; // A potentially invalid assumption: that Mono means running on a POSIX-compliant system.

		private static readonly string EXECUTABLE_DIRECTORY = Path.GetDirectoryName(Assembly.GetEntryAssembly().CodeBase.Replace(ON_POSIX_COMPLAINT_OS ? "file:/" : "file:///", string.Empty));

		private static readonly string DEFAULT_INI_FILE = Path.Combine(EXECUTABLE_DIRECTORY, "WHIP_LRU.ini");

		private static readonly string COMPILED_BY = "?mono?"; // Replaced during automatic packaging.

		private static readonly string DEFAULT_DB_FOLDER_PATH = "localStorage";

		private static readonly string DEFAULT_WRITECACHE_FILE_PATH = "whiplru.wcache";

		private static readonly uint DEFAULT_WRITECACHE_RECORD_COUNT = 1024U * 1024U * 1024U/*1GB*/ / 17 /*WriteCacheNode.BYTE_SIZE*/;

		private static readonly uint DEFAULT_DB_PARTITION_INTERVAL_MINUTES = 60 * 24/*1day*/;

		private static readonly Dictionary<string, IAssetServer> _assetServersByName = new Dictionary<string, IAssetServer>();

		public static int Main(string[] args) {
			// First line, hook the appdomain to the crash reporter
			AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

			var waitHandle = new EventWaitHandle(false, EventResetMode.AutoReset, "70a9f94f-59e8-4073-93ab-00aaacc26111", out var createdNew);

			if (!createdNew) {
				LOG.Error("Server process already started, please stop that server first.");
				return 2;
			}

			// Add the arguments supplied when running the application to the configuration
			var configSource = new ArgvConfigSource(args);

			// Commandline switches
			configSource.AddSwitch("Startup", "inifile");
			configSource.AddSwitch("Startup", "logconfig");
			configSource.AddSwitch("Startup", "pidfile");

			var startupConfig = configSource.Configs["Startup"];

			var pidFileManager = new PIDFileManager(startupConfig.GetString("pidfile", string.Empty));

			// Configure Log4Net
			{
				var logConfigFile = startupConfig.GetString("logconfig", string.Empty);
				if (string.IsNullOrEmpty(logConfigFile)) {
					XmlConfigurator.Configure();
					LogBootMessage();
					LOG.Info("Configured log4net using ./WHIP_LRU.exe.config as the default.");
				}
				else {
					XmlConfigurator.Configure(new FileInfo(logConfigFile));
					LogBootMessage();
					LOG.Info($"Configured log4net using \"{logConfigFile}\" as configuration file.");
				}
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
			UnixSignal[] signals = null;
			if (ON_POSIX_COMPLAINT_OS) {
				signals = new [] {
					new UnixSignal(Signum.SIGINT),
					new UnixSignal(Signum.SIGTERM),
					new UnixSignal(Signum.SIGHUP),
				};
			}
			else {
				Console.CancelKeyPress += (sender, cargs) => {
					LOG.Debug("CTRL-C pressed, terminating.");
					isRunning = false;
					whipLru?.Stop();

					cargs.Cancel = true;
					waitHandle.Set();
				};
			}

			while (isRunning) {
				// Dump any known servers, we're going to reconfigure them.
				foreach (var server in _assetServersByName.Values) {
					server.Dispose();
				}
				// TODO: might need to double buffer these, or something, so that old ones can finish out before being disposed.

				// Read in the ini file
				ReadConfigurationFromINI(configSource);

				// Read in a config list that lists the priority order of servers and their settings.

				var configRead = configSource.Configs["AssetsRead"];
				var configWrite = configSource.Configs["AssetsWrite"];

				var serversRead = GetServers(configSource, configRead, _assetServersByName);
				var serversWrite = GetServers(configSource, configWrite, _assetServersByName);

				var chattelConfigRead = GetConfig(configRead, serversRead);
				var chattelConfigWrite = GetConfig(configWrite, serversWrite);

				var serverConfig = configSource.Configs["Server"];

				var address = serverConfig?.GetString("Address", WHIPServer.DEFAULT_ADDRESS) ?? WHIPServer.DEFAULT_ADDRESS;
				var port = (uint?)serverConfig?.GetInt("Port", (int)WHIPServer.DEFAULT_PORT) ?? WHIPServer.DEFAULT_PORT;
				var password = serverConfig?.GetString("Password", WHIPServer.DEFAULT_PASSWORD);
				if (password == null) { // Would only be null if serverConfig was null or DEFAULT_PASSWORD is null.  Why not use the ?? operator? Compiler didn't like it.
					password = WHIPServer.DEFAULT_PASSWORD;
				}
				var listenBacklogLength = (uint?)serverConfig?.GetInt("ConnectionQueueLength", (int)WHIPServer.DEFAULT_BACKLOG_LENGTH) ?? WHIPServer.DEFAULT_BACKLOG_LENGTH;

				var localStorageConfig = configSource.Configs["LocalStorage"];

				var maxAssetLocalStorageDiskSpaceByteCount = (ulong?)localStorageConfig?.GetLong("MaxDiskSpace", (long)AssetLocalStorageLmdbPartitionedLRU.DB_MAX_DISK_BYTES_MIN_RECOMMENDED) ?? AssetLocalStorageLmdbPartitionedLRU.DB_MAX_DISK_BYTES_MIN_RECOMMENDED;
				var negativeCacheItemLifetime = TimeSpan.FromSeconds((uint?)localStorageConfig?.GetInt("NegativeCacheItemLifetimeSeconds", (int)StorageManager.DEFAULT_NC_LIFETIME_SECONDS) ?? StorageManager.DEFAULT_NC_LIFETIME_SECONDS);
				var partitionInterval = TimeSpan.FromMinutes((uint?)localStorageConfig?.GetInt("MinutesBetweenDatabasePartitions", (int)DEFAULT_DB_PARTITION_INTERVAL_MINUTES) ?? DEFAULT_DB_PARTITION_INTERVAL_MINUTES);

				var readerLocalStorage = new AssetLocalStorageLmdbPartitionedLRU(
					chattelConfigRead,
					maxAssetLocalStorageDiskSpaceByteCount,
					partitionInterval
				);
				var chattelReader = new ChattelReader(chattelConfigRead, readerLocalStorage); // TODO: add purge flag to CLI
				var chattelWriter = new ChattelWriter(chattelConfigWrite, readerLocalStorage); // add purge flag to CLI

				var storageManager = new StorageManager(
					readerLocalStorage,
					negativeCacheItemLifetime,
					chattelReader,
					chattelWriter
				);

				whipLru = new WhipLru(
					address,
					port,
					password,
					pidFileManager,
					storageManager,
					listenBacklogLength
				);

				whipLru.Start();

				if (signals != null) {
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
						default:
							// Signal unknown, ignore it.
						break;
					}
				}
				else {
					waitHandle.WaitOne();
				}
			}

			foreach (var server in _assetServersByName.Values) {
				server.Dispose();
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
				LOG.Info($"Attempting to read configuration file {Path.GetFullPath(iniFileName)}");
				startupConfig.ConfigSource.Merge(new IniConfigSource(iniFileName));
				LOG.Info($"Success reading configuration file.");
				found_at_given_path = true;
			}
			catch {
				LOG.Warn($"Failure reading configuration file at {Path.GetFullPath(iniFileName)}");
			}

			if (!found_at_given_path) {
				// Combine with true path to binary and try again.
				iniFileName = Path.Combine(EXECUTABLE_DIRECTORY, iniFileName);

				try {
					LOG.Info($"Attempting to read configuration file from installation path {Path.GetFullPath(iniFileName)}");
					startupConfig.ConfigSource.Merge(new IniConfigSource(iniFileName));
					LOG.Info($"Success reading configuration file.");
				}
				catch {
					LOG.Fatal($"Failure reading configuration file at {Path.GetFullPath(iniFileName)}");
				}
			}
		}

		private static IEnumerable<IEnumerable<IAssetServer>> GetServers(IConfigSource configSource, IConfig assetConfig, Dictionary<string, IAssetServer> serverList) {
			var serialParallelServerSources = assetConfig?
				.GetString("Servers", string.Empty)
				.Split(',')
				.Where(parallelSources => !string.IsNullOrWhiteSpace(parallelSources))
				.Select(parallelSources => parallelSources
					.Split('&')
					.Where(source => !string.IsNullOrWhiteSpace(source))
					.Select(source => source.Trim())
				)
				.Where(parallelSources => parallelSources.Any())
			;

			var serialParallelAssetServers = new List<List<IAssetServer>>();

			if (serialParallelServerSources != null && serialParallelServerSources.Any()) {
				foreach (var parallelSources in serialParallelServerSources) {
					var parallelServerConnectors = new List<IAssetServer>();
					foreach (var sourceName in parallelSources) {
						var sourceConfig = configSource.Configs[sourceName];
						var type = sourceConfig?.GetString("Type", string.Empty)?.ToLower(System.Globalization.CultureInfo.InvariantCulture);

						if (!serverList.TryGetValue(sourceName, out var serverConnector)) {
							try {
								switch (type) {
									case "whip":
										serverConnector = new AssetServerWHIP(
											sourceName,
											sourceConfig.GetString("Host", string.Empty),
											sourceConfig.GetInt("Port", 32700),
											sourceConfig.GetString("Password", "changeme") // Yes, that's the default password for WHIP.
										);
										break;
									case "cf":
										serverConnector = new AssetServerCF(
											sourceName,
											sourceConfig.GetString("Username", string.Empty),
											sourceConfig.GetString("APIKey", string.Empty),
											sourceConfig.GetString("DefaultRegion", string.Empty),
											sourceConfig.GetBoolean("UseInternalURL", true),
											sourceConfig.GetString("ContainerPrefix", string.Empty)
										);
										break;
									default:
										LOG.Warn($"Unknown asset server type in section [{sourceName}].");
										break;
								}

								serverList.Add(sourceName, serverConnector);
							}
							catch (SocketException e) {
								LOG.Error($"Asset server of type '{type}' defined in section [{sourceName}] failed setup. Skipping server.", e);
							}
						}

						if (serverConnector != null) {
							parallelServerConnectors.Add(serverConnector);
						}
					}

					if (parallelServerConnectors.Any()) {
						serialParallelAssetServers.Add(parallelServerConnectors);
					}
				}
			}
			else {
				LOG.Warn("Servers empty or not specified. No asset server sections configured.");
			}

			return serialParallelAssetServers;
		}

		private static ChattelConfiguration GetConfig(IConfig assetConfig, IEnumerable<IEnumerable<IAssetServer>> serialParallelAssetServers) {
			// Set up local storage
			var localStoragePathRead = assetConfig?.GetString("DatabaseFolderPath", DEFAULT_DB_FOLDER_PATH) ?? DEFAULT_DB_FOLDER_PATH;

			DirectoryInfo localStorageFolder = null;

			if (string.IsNullOrWhiteSpace(localStoragePathRead)) {
				LOG.Info($"DatabaseFolderPath is empty, local storage of assets disabled.");
			}
			else if (!Directory.Exists(localStoragePathRead)) {
				LOG.Info($"DatabaseFolderPath folder does not exist, local storage of assets disabled.");
			}
			else {
				localStorageFolder = new DirectoryInfo(localStoragePathRead);
				LOG.Info($"Local storage of assets enabled at {localStorageFolder.FullName}");
			}

			// Set up write cache
			var writeCachePath = assetConfig?.GetString("WriteCacheFilePath", DEFAULT_WRITECACHE_FILE_PATH) ?? DEFAULT_WRITECACHE_FILE_PATH;
			var writeCacheRecordCount = (uint)Math.Max(0, assetConfig?.GetLong("WriteCacheRecordCount", DEFAULT_WRITECACHE_RECORD_COUNT) ?? DEFAULT_WRITECACHE_RECORD_COUNT);

			if (string.IsNullOrWhiteSpace(writeCachePath) || writeCacheRecordCount <= 0 || localStorageFolder == null) {
				LOG.Warn($"WriteCacheFilePath is empty, WriteCacheRecordCount is zero, or caching is disabled. Crash recovery will be compromised.");
			}
			else {
				var writeCacheFile = new FileInfo(writeCachePath);
				LOG.Info($"Write cache enabled at {writeCacheFile.FullName} with {writeCacheRecordCount} records.");
			}

			return new ChattelConfiguration(localStoragePathRead, writeCachePath, writeCacheRecordCount, serialParallelAssetServers);
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

				msg = $"APPLICATION EXCEPTION DETECTED: {e}\n" +
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
				LOG.Error("Exception launching CrashReporter.", ex);
			}
			finally {
				_isHandlingException = false;

				if (e.IsTerminating) {

					foreach (var server in _assetServersByName.Values) {
						try {
							server.Dispose();
						}
#pragma warning disable RECS0022 // A catch clause that catches System.Exception and has an empty body
						catch {
							// Ignore.
						}
#pragma warning restore RECS0022 // A catch clause that catches System.Exception and has an empty body
					}


					// Preempt to not show a pile of puke if console was disabled.
					Environment.Exit(1);
				}
			}
		}

		#endregion
	}
}
