﻿using Quobject.SocketIoClientDotNet.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Collections.Immutable;
using Quobject.SocketIoClientDotNet.EngineIoClientDotNet.Client.Transports;
using IO = Quobject.SocketIoClientDotNet.Client.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Route4MeSDK.DataTypes;
using Newtonsoft.Json.Serialization;
using System.Diagnostics;
using System.Net.Http;
using System.Security.Authentication;

namespace Route4MeSDK.FastProcessing
{
    /// <summary>
    /// The class for the geocoding of bulk addresses
    /// </summary>
    public class FastBulkGeocoding : Connection
    {
        private ManualResetEvent manualResetEvent = null;
        private ManualResetEvent mainResetEvent = null;
        private Socket socket;
        public string Message;
        //private int Number;
        //private bool Flag;
        public static Connection con = new Connection();
        private int? requestedAddresses;
        private int nextDownloadStage;
        private int loadedAddressesCount;
        string TEMPORARY_ADDRESSES_STORAGE_ID;

        FastFileReading fileReading;

        bool largeJsonFileProcessingIsDone;
        bool geocodedAddressesDownloadingIsDone;

        List<AddressGeocoded> savedAddresses;

        public string ApiKey { get; set; }

        public FastBulkGeocoding(string apiKey, bool EnableTraceSource = false)
        {
            if (ApiKey!="") ApiKey = apiKey;
            Quobject.SocketIoClientDotNet.TraceSourceTools.LogTraceSource.TraceSourceLogging(EnableTraceSource);
        }

        #region // Addresses chunk's geocoding is finished event handler
        public event EventHandler<AddressesChunkGeocodedArgs> AddressesChunkGeocoded;

        protected virtual void OnAddressesChunkGeocoded(AddressesChunkGeocodedArgs e)
        {
            EventHandler< AddressesChunkGeocodedArgs> handler = AddressesChunkGeocoded;

            handler?.Invoke(this, e);
        }

        public class AddressesChunkGeocodedArgs : EventArgs
        {
            public List<AddressGeocoded> LsAddressesChunkGeocoded { get; set; }
        }

        public delegate void AddressesChunkGeocodedEventHandler(object sender, AddressesChunkGeocodedArgs e);
        #endregion

        #region // geocoding is finished event handler
        public event EventHandler<GeocodingIsFinishedArgs> GeocodingIsFinished;

        protected virtual void OnGeocodingIsFinished(GeocodingIsFinishedArgs e)
        {
            EventHandler<GeocodingIsFinishedArgs> handler = GeocodingIsFinished;

            handler?.Invoke(this, e);
        }

        public class GeocodingIsFinishedArgs : EventArgs
        {
            public bool IsFinished { get; set; }
        }

        public delegate void GeocodingIsFinishedEventHandler(object sender, AddressesChunkGeocodedArgs e);
        #endregion


        /// <summary>
        /// Upload and geocode large JSON file
        /// </summary>
        /// <param name="fileName">JSON file name</param>
        public void UploadAndGeocodeLargeJsonFile(string fileName)
        {
            Route4MeManager route4Me = new Route4MeManager(ApiKey);

            largeJsonFileProcessingIsDone = false;

            fileReading = new FastFileReading
            {
                JsonObjectsChunkSize = 200
            };

            savedAddresses = new List<AddressGeocoded>();

            fileReading.JsonFileChunkIsReady += FileReading_JsonFileChunkIsReady;

            fileReading.JsonFileReadingIsDone += FileReading_JsonFileReadingIsDone;

            mainResetEvent = new ManualResetEvent(false);

            fileReading.ReadingChunksFromLargeJsonFile(fileName);

        }

        /// <summary>
        /// Event handler for the JsonFileReadingIsDone event
        /// </summary>
        /// <param name="sender">Event raiser object</param>
        /// <param name="e">Event arguments of the type JsonFileReadingIsDoneArgs</param>
        private void FileReading_JsonFileReadingIsDone(object sender, FastFileReading.JsonFileReadingIsDoneArgs e)
        {
            bool isDone = e.IsDone;
            if (isDone)
            {
                largeJsonFileProcessingIsDone = true;
                mainResetEvent.Set();
                if (geocodedAddressesDownloadingIsDone)
                {
                    OnGeocodingIsFinished(new GeocodingIsFinishedArgs() { IsFinished = true });
                }
                // fire here event for external (test) code
            }

        }


        /// <summary>
        /// Event handler for the JsonFileChunkIsReady event
        /// </summary>
        /// <param name="sender">Event raiser object</param>
        /// <param name="e">Event arguments of the type JsonFileChunkIsReadyArgs</param>
        private void FileReading_JsonFileChunkIsReady(object sender, FastFileReading.JsonFileChunkIsReadyArgs e)
        {
            string jsonAddressesChunk = e.AddressesChunk;

            var uploadAddressesResponse = UploadAddressesToTemporaryStorage(jsonAddressesChunk);

            if (uploadAddressesResponse!=null)
            {
                string tempAddressesStorageID = uploadAddressesResponse.OptimizationProblemId;
                int addressesInChunk = (int)uploadAddressesResponse.AddressCount;

                if (addressesInChunk < fileReading.JsonObjectsChunkSize) requestedAddresses = addressesInChunk; // last chunk

                DownloadGeocodedAddresses(tempAddressesStorageID, addressesInChunk);
            }
            
        }

        /// <summary>
        /// Upload JSON addresses to a temporary storage
        /// </summary>
        /// <param name="streamSource">Input stream source - file name or JSON text</param>
        /// <returns>Response object of the type uploadAddressesToTemporaryStorageResponse</returns>
        public Route4MeManager.UploadAddressesToTemporaryStorageResponse UploadAddressesToTemporaryStorage(string streamSource)
        {
            Route4MeManager route4Me = new Route4MeManager(ApiKey);

            string jsonText = streamSource.Contains("{") && streamSource.Contains("}") ? streamSource : ReadJsonTextFromLargeJsonFileOfAddresses(streamSource);

            Route4MeManager.UploadAddressesToTemporaryStorageResponse uploadResponse =
                route4Me.UploadAddressesToTemporaryStorage(jsonText, out string errorString);

            if (!uploadResponse.Status) return null;
            if (uploadResponse == null) Console.WriteLine(errorString);

            return uploadResponse ?? null;
        }

        /// <summary>
        /// Geocode and download the addresses from the temporary storage.
        /// </summary>
        /// <param name="temporaryAddressesStorageID">ID of the temporary storage</param>
        /// <param name="addressesInFile">Chunk size of the addresses to be geocoded</param>
        public async void DownloadGeocodedAddresses(string temporaryAddressesStorageID, int? addressesInFile)
        {
            WinHttpHandler httpHandler = new WinHttpHandler()
            {
                SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls11 | SslProtocols.Tls
            };

            using (httpHandler)
            {
                geocodedAddressesDownloadingIsDone = false;

                savedAddresses = new List<AddressGeocoded>();

                TEMPORARY_ADDRESSES_STORAGE_ID = temporaryAddressesStorageID;

                if (addressesInFile != null) requestedAddresses = (int)addressesInFile;

                manualResetEvent = new ManualResetEvent(false);

                var options = CreateOptions();
                options.Path = "/socket.io";
                options.Host = "validator.route4me.com/";
                options.AutoConnect = true;
                options.IgnoreServerCertificateValidation = true;
                options.Timeout = 60000;
                options.Upgrade = true;
                options.ForceJsonp = true;
                options.Transports = ImmutableList.Create<string>(new string[] { Polling.NAME, WebSocket.NAME });

                var uri = CreateUri();
                socket = IO.Socket(uri, options);

                socket.On("error", (message) =>
                {
                    Debug.Print("Error -> " + message);
                    Thread.Sleep(500);
                    manualResetEvent.Set();
                    socket.Disconnect();
                    httpHandler.Dispose();
                });

                socket.On(Socket.EVENT_ERROR, (e) =>
                {
                    var exception = (Quobject.SocketIoClientDotNet.EngineIoClientDotNet.Client.EngineIOException)e;
                    Console.WriteLine("EVENT_ERROR. " + exception.Message);
                    Console.WriteLine("BASE EXCEPTION. " + exception.GetBaseException());
                    Console.WriteLine("DATA COUNT. " + exception.Data.Count);
                    socket.Disconnect();
                    manualResetEvent.Set();
                    httpHandler.Dispose();
                });

                socket.On(Socket.EVENT_MESSAGE, (message) =>
                {
                    Thread.Sleep(500);
                });

                socket.On("data", (d) =>
                {
                    Thread.Sleep(1000);
                });

                socket.On(Socket.EVENT_CONNECT, () =>
                {
                    Thread.Sleep(500);
                });

                socket.On(Socket.EVENT_DISCONNECT, () =>
                {
                    Thread.Sleep(700);
                });

                socket.On(Socket.EVENT_RECONNECT_ATTEMPT, () =>
                {
                    Thread.Sleep(1500);
                });

                socket.On("addresses_bulk", (addresses_chunk) =>
                {
                    string jsonChunkText = addresses_chunk.ToString();

                    List<string> errors = new List<string>();

                    JsonSerializerSettings jsonSettings = new JsonSerializerSettings()
                    {
                        Error = delegate (object sender, ErrorEventArgs args)
                        {
                            errors.Add(args.ErrorContext.Error.Message);
                            args.ErrorContext.Handled = true;
                        },
                        NullValueHandling = NullValueHandling.Ignore
                    };

                    var addressesChunk = JsonConvert.DeserializeObject<AddressGeocoded[]>(jsonChunkText, jsonSettings);

                    if (errors.Count > 0)
                    {
                        Debug.Print("Json serializer errors:");
                        foreach (string errMessage in errors) Debug.Print(errMessage);
                    }

                    savedAddresses = savedAddresses.Concat(addressesChunk).ToList();

                    loadedAddressesCount += addressesChunk.Length;

                    if (loadedAddressesCount == nextDownloadStage)
                    {
                        Download(loadedAddressesCount);
                    }

                    if (loadedAddressesCount == requestedAddresses)
                    {
                        socket.Emit("disconnect", TEMPORARY_ADDRESSES_STORAGE_ID);
                        loadedAddressesCount = 0;
                        AddressesChunkGeocodedArgs args = new AddressesChunkGeocodedArgs() { LsAddressesChunkGeocoded = savedAddresses };
                        OnAddressesChunkGeocoded(args);

                        manualResetEvent.Set();

                        geocodedAddressesDownloadingIsDone = true;

                        if (largeJsonFileProcessingIsDone)
                        {
                            OnGeocodingIsFinished(new GeocodingIsFinishedArgs() { IsFinished = true });
                        }

                        socket.Close();
                    }

                });

                socket.On("geocode_progress", (message) =>
                {
                    var progressMessage = JsonConvert.DeserializeObject<Progress>(message.ToString());

                    if (progressMessage.Total == progressMessage.Done)
                    {
                        if (requestedAddresses == null) requestedAddresses = progressMessage.Total;
                        Download(0);
                    }
                });

                var jobj = JObject.Parse(@"{ ""temporary_addresses_storage_id"": ""+TEMPORARY_ADDRESSES_STORAGE_ID+@"", ""force_restart"": true }");

                var _args = new List<object> { jobj };

                try
                {
                    socket.Emit("geocode", _args);
                }
                catch (Exception ex)
                {
                    Debug.Print("Socket connection failed. " + ex.Message);
                }

                manualResetEvent.WaitOne();
            };
        }

        /// <summary>
        /// Download chunk of the geocoded addresses
        /// </summary>
        /// <param name="start">Download addresses starting from</param>
        public void Download(int start)
        {
            int bufferFailSafeMaxAddresses = 100;
            int chunkSize = (int)Math.Round((decimal)(Math.Min(200, Math.Max(10, (requestedAddresses!=null ? (int)requestedAddresses : 0) / 100))));
            int chunksLimit = (int)Math.Ceiling(((decimal)(bufferFailSafeMaxAddresses / chunkSize)));

            int maxAddressesToBeDownloaded = chunkSize * chunksLimit;
            nextDownloadStage = loadedAddressesCount + maxAddressesToBeDownloaded;

            var jobj = new JObject
            {
                new KeyValuePair<string, JToken> ("temporary_addresses_storage_id", TEMPORARY_ADDRESSES_STORAGE_ID),
                new KeyValuePair<string, JToken> ("from_index", start),
                new KeyValuePair<string, JToken> ("chunks_limit", chunksLimit),
                new KeyValuePair<string, JToken> ("chunk_size", chunkSize)
            };

            var _args = new List<object> { jobj };

            socket.Emit("download", _args);
        }

        public string ReadJsonTextFromLargeJsonFileOfAddresses(String sFileName)
        {
            FastFileReading fileRead = new FastFileReading();

            return fileRead != null ? fileRead.ReadJsonTextFromFile(sFileName) : String.Empty;
        }
    }

    /// <summary>
    /// Response class of the received event about geocoding progress
    /// </summary>
    class Progress
    {
        public int Total { get; set; }

        public int Done { get; set; }
    }
}
