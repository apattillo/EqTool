﻿using EQTool.Services;
using EQTool.ViewModels;
using EQToolShared.Enums;
using EQToolShared.HubModels;
using EQToolShared.Map;
using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Timers;

namespace EQTool.Models
{

    public interface ISignalrPlayerHub
    {
        event EventHandler<SignalrPlayer> PlayerLocationEvent;
        event EventHandler<SignalrPlayer> PlayerDisconnected;
        void PushPlayerLocationEvent(SignalrPlayer player);
        void PushPlayerDisconnected(SignalrPlayer player);
    }

    public class SignalrPlayerHub : ISignalrPlayerHub
    {
        private readonly HubConnection connection;
        private readonly ActivePlayer activePlayer;
        private readonly LogParser logParser;
        private readonly IAppDispatcher appDispatcher;
        private readonly Timer timer;
        private SignalrPlayer LastPlayer;
        private readonly SpellWindowViewModel spellWindowViewModel;
        private Servers? LastServer;
        public SignalrPlayerHub(IAppDispatcher appDispatcher, LogParser logParser, ActivePlayer activePlayer, SpellWindowViewModel spellWindowViewModel)
        {
            this.appDispatcher = appDispatcher;
            this.activePlayer = activePlayer;
            this.logParser = logParser;
            this.spellWindowViewModel = spellWindowViewModel;
            var url = "https://www.pigparse.org/EqToolMap";
            connection = new HubConnectionBuilder()
              .WithUrl(url)
              .WithAutomaticReconnect()
              .Build();
            connection.On("PlayerLocationEvent", (SignalrPlayer p) =>
                {
                    this.PushPlayerLocationEvent(p);
                });
            connection.On("PlayerDisconnected", (SignalrPlayer p) =>
            {
                this.PushPlayerDisconnected(p);
            });
            connection.On("AddCustomTrigger", (SignalrCustomTimer p) =>
            {
                this.AddCustomTrigger(p);
            });
            connection.Closed += async (error) =>
              {
                  await Task.Delay(new Random().Next(0, 5) * 1000);
                  await ConnectWithRetryAsync(connection);
              };

            try
            {
                Task.Factory.StartNew(async () =>
                {
                    await ConnectWithRetryAsync(connection);
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }

            this.logParser.PlayerLocationEvent += LogParser_PlayerLocationEvent;
            this.logParser.CampEvent += LogParser_CampEvent;
            timer = new Timer();
            timer.Elapsed += Timer_Elapsed;
            timer.Interval = 1000 * 10;
            timer.Start();
        }

        private void InvokeAsync<T>(string name, T obj)
        {
            if (connection.State == HubConnectionState.Connected)
            {
                connection.InvokeAsync(name, obj);
            }
        }

        private void LogParser_CampEvent(object sender, LogParser.CampEventArgs e)
        {
            this.LastPlayer = null;
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (this.LastPlayer != null && connection?.State == HubConnectionState.Connected && this.activePlayer?.Player?.Server != null)
            {
                if ((DateTime.UtcNow - this.logParser.LastYouActivity).TotalMinutes > 5)
                {
                    this.LastPlayer = null;
                }
                else
                {
                    InvokeAsync("PlayerLocationEvent", this.LastPlayer);
                }
            }
        }

        public event EventHandler<SignalrPlayer> PlayerLocationEvent;
        public event EventHandler<SignalrPlayer> PlayerDisconnected;

        private static async Task<bool> ConnectWithRetryAsync(HubConnection connection)
        {
            while (true)
            {
                try
                {
                    Debug.WriteLine("Beg StartAsync");
                    await connection.StartAsync();
                    Debug.WriteLine("Connected StartAsync");
                    return true;
                }
                catch
                {
                    Debug.WriteLine("Failed StartAsync");
                    await Task.Delay(5000);
                }
            }
        }

        public void PushPlayerDisconnected(SignalrPlayer p)
        {
            if (!(p.Server == this.activePlayer?.Player?.Server && p.Name == this.activePlayer?.Player?.Name))
            {
                Debug.WriteLine($"PlayerDisconnected {p.Name}");
                this.appDispatcher.DispatchUI(() =>
                {
                    PlayerDisconnected?.Invoke(this, p);
                });
            }
        }

        public void AddCustomTrigger(SignalrCustomTimer p)
        {
            if (p.Server == this.activePlayer?.Player?.Server)
            {
                Debug.WriteLine($"AddCustomTrigger {p.Name}");
                this.appDispatcher.DispatchUI(() =>
                {
                    this.spellWindowViewModel.TryAddCustom(p);
                });
            }
        }

        public void PushPlayerLocationEvent(SignalrPlayer p)
        {
            if (!(p.Server == this.activePlayer?.Player?.Server && p.Name == this.activePlayer?.Player?.Name))
            {
                Debug.WriteLine($"PlayerLocationEvent {p.Name}");
                this.appDispatcher.DispatchUI(() =>
                {
                    PlayerLocationEvent?.Invoke(this, p);
                });
            }
        }

        private void LogParser_PlayerLocationEvent(object sender, LogParser.PlayerLocationEventArgs e)
        {
            if (this.activePlayer?.Player?.Server != null)
            {
                this.LastPlayer = new SignalrPlayer
                {
                    Zone = this.activePlayer.Player.Zone,
                    GuildName = this.activePlayer.Player.GuildName,
                    PlayerClass = this.activePlayer.Player.PlayerClass,
                    Server = this.activePlayer.Player.Server.Value,
                    MapLocationSharing = this.activePlayer.Player.MapLocationSharing.Value,
                    Name = this.activePlayer.Player.Name,
                    TrackingDistance = this.activePlayer.Player.TrackingDistance,
                    X = e.Location.X,
                    Y = e.Location.Y,
                    Z = e.Location.Z
                };

                InvokeAsync("PlayerLocationEvent", this.LastPlayer);
            }
        }
    }
}
