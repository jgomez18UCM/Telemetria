﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using System.Threading;
using System.Collections.Concurrent;
using System.IO;

namespace Telemetria
{
    public class Tracker
    {
        private static Tracker? _instance;
        public static Tracker? Instance { get { return _instance; } }
        CancellationTokenSource? _cancellationTokenSource;

        private ConcurrentQueue<Event> eventsQueue;
        private Thread persistThread;
        private List<IPersistance> _persisters;
        private string userId;
        private string sessionId;
        private int gameId;
        private static string? telemetryDataPath;
        public static string? TelemetryDataPath { get; }
        private const UInt32 SAVING_FREQ = 10000;

        private Tracker(string userId)
        {
            eventsQueue = new ConcurrentQueue<Event>();
            this.userId = userId;
            this.sessionId = Guid.NewGuid().ToString();
            _persisters = new List<IPersistance>();
            addFilePersister(new SerializerJSON(), "events.json");
            gameId = -1;
            _cancellationTokenSource = new CancellationTokenSource();
            persistThread = new Thread(() => ThreadLoop(_cancellationTokenSource.Token));
        }

        /// <summary>
        /// Inicializa el Tracker
        /// </summary>
        /// <param name="userId">Id única del usuario</param>
        /// <param name="appDataPath">
        /// Path al directorio de datos del juego (sin barra separadora al final,
        /// para conseguirlo en Unity se puede usar Application.persistentDataPath</param>
        /// <returns>Returns true if the instance was initialized correctly, false otherwise</returns>
        public static bool Init(string userId, string appDataPath)
        {
            telemetryDataPath = $"{appDataPath}/Telemetry/";
            Directory.CreateDirectory(telemetryDataPath);
            _instance = new Tracker(userId);
            _instance.TrackEvent(new StartSession());
            
            try
            {
                _instance.StartThread();
            }
            catch (Exception e)
            {
                return false;
            }
            return true;
        }
        private void StartThread()
        {
            persistThread.Start();
        }
        public static void Close()
        {
            if (_instance == null)
            {
                return;
            }
            _instance?.End();

        }

        public void addPersistance(IPersistance persistance)
        {
            _persisters.Add(persistance);
        }

        public void addFilePersister(ISerializer serializer, string file)
        {
            _persisters.Add(new FilePersistance(serializer, $"{telemetryDataPath}{file}"));
        }
        private void End()
        {
            TrackEvent(new EndSession());
            _cancellationTokenSource?.Cancel();
            persistThread.Join();
            SaveAll();
            foreach(var persistance in _persisters)
            {
                persistance.Close();
            }
        }
        public void TrackEvent(in Event evt)
        {
            //Preparar el evento con los datos de la sesion
            if (evt.event_type == "StartSession")
            {
                gameId++;
            }
            evt.id_session = sessionId;
            evt.id_user = userId;
            evt.id_game = HashCode.Combine(gameId, userId, sessionId).ToString();
            eventsQueue.Enqueue(evt);
        }
        private void ThreadLoop(CancellationToken tk)
        {
            while (true)
            {
                int result = WaitHandle.WaitAny(new WaitHandle[] { tk.WaitHandle }, TimeSpan.FromMilliseconds(SAVING_FREQ));

                if (result != WaitHandle.WaitTimeout)
                    break;
                SaveAll();
            }
        }
        private void SaveAll()
        {
            while (eventsQueue.TryDequeue(out var evt))
            {
                foreach (IPersistance persister in _persisters)
                {
                    persister.Save(evt);
                }
            }
        }
    }
}
