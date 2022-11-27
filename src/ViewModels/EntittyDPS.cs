﻿using EQTool.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace EQTool.ViewModels
{
    public class EntittyDPS : INotifyPropertyChanged
    {
        private string _SourceName = string.Empty;

        public string SourceName
        {
            get => _SourceName;
            set
            {
                _SourceName = value;
                BackGroundColor = _SourceName == EQSpells.SpaceYou ? Brushes.LightBlue : Brushes.DarkSeaGreen;
                OnPropertyChanged();
            }
        }

        private string _TargetName = string.Empty;

        public string TargetName
        {
            get => _TargetName;
            set
            {
                _TargetName = value;
                BackGroundColor = _TargetName == EQSpells.SpaceYou ? Brushes.LightBlue : Brushes.DarkSeaGreen;
                OnPropertyChanged();
            }
        }

        public bool IsDead = false;

        private DateTime _StartTime = DateTime.Now;

        public DateTime StartTime
        {
            get => _StartTime;
            set
            {
                _StartTime = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DPS));
                OnPropertyChanged(nameof(TotalSeconds));
            }
        }

        public void UpdateDps()
        {
            OnPropertyChanged(nameof(TotalTwelveSecondDamage));
            OnPropertyChanged(nameof(TwelveSecondDPS));
            OnPropertyChanged(nameof(TotalDamage));
            OnPropertyChanged(nameof(DPS));
        }


        public int TotalSeconds => (int)(DateTime.Now - _StartTime).TotalSeconds;

        public void AddDamage(DamagePerTime damage)
        {
            Damage.Add(damage);
            TotalTwelveSecondDamage = 0;
            if (damage.Damage > HighestHit)
            {
                HighestHit = damage.Damage;
                OnPropertyChanged(nameof(HighestHit));
            }
            var timestampstep = Damage.FirstOrDefault().TimeStamp;
            var highesttempdmg = GetDamangeAfter(0, timestampstep);
            TotalTwelveSecondDamage = Math.Max(TotalTwelveSecondDamage, highesttempdmg);

            for (var i = 0; i < Damage.Count; i++)
            {
                var item = Damage[i];
                if ((item.TimeStamp - timestampstep).TotalMilliseconds > 1000)
                {
                    highesttempdmg = GetDamangeAfter(i, timestampstep);
                    timestampstep = item.TimeStamp;
                    TotalTwelveSecondDamage = Math.Max(TotalTwelveSecondDamage, highesttempdmg);
                }
            }

            Trailing5SecondDamage = Damage.Where(a => a.TimeStamp >= DateTime.Now.AddMilliseconds(-5000)).Sum(a => a.Damage);
            TotalDamage = Damage.Sum(a => a.Damage);
            UpdateDps();
        }

        private int GetDamangeAfter(int i, DateTime lasttimestamp)
        {
            var totaldamage = 0;
            for (var j = i; j < Damage.Count; j++)
            {
                var inneritem = Damage[j];
                if ((lasttimestamp - inneritem.TimeStamp).TotalMilliseconds > 12000)
                {
                    break;
                }
                totaldamage += inneritem.Damage;
            }

            return totaldamage;
        }

        public class DamagePerTime
        {
            public DateTime TimeStamp { get; set; }

            public int Damage { get; set; }
        }

        private readonly List<DamagePerTime> Damage = new List<DamagePerTime>();

        private int Trailing5SecondDamage { get; set; } = 0;
        public int TotalDamage { get; set; }
        public int TotalTwelveSecondDamage { get; set; }
        public int HighestHit { get; set; }
        public SolidColorBrush BackGroundColor { get; set; }

        public int TwelveSecondDPS => (TotalTwelveSecondDamage > 0) ? (int)(TotalTwelveSecondDamage / (double)12) : 0;
        public int DPS => (Trailing5SecondDamage > 0 && TotalSeconds > 0) ? (int)(Trailing5SecondDamage / (double)5) : 0;

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
