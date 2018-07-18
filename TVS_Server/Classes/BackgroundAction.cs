using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace TVS_Server
{
    class BackgroundAction {

        private static Dictionary<long, BackgroundAction> backgroundActions = new Dictionary<long, BackgroundAction>();
        private static long idCount = 0;

        public static List<BackgroundAction> GetActions() {
            return backgroundActions.Values.ToList();
        }

        private static void AddAction(BackgroundAction action) {
            backgroundActions.Add(action.id,action);
        }

        private static void RemoveAction(BackgroundAction action) {
            if (backgroundActions.ContainsKey(action.id)) {
                backgroundActions.Remove(action.id);
            }
        }

        public static void UpdateAction(BackgroundAction action) {
            if (backgroundActions.ContainsKey(action.id)) {
                backgroundActions[action.id] = action;
            }
        }

        private long id;

        private int _value = 0;
        private string _name;
        private int _maxValue;
        public string Name { get { return _name; } set { _name = value; UpdateAction(this); } }
        public int Value {
            get {
                return _value;
            }
            set {
                _value = value;
                SetValue(value);
                UpdateAction(this);
            }
        }
        public int MaxValue { get { return _maxValue; } set { _maxValue = value; UpdateAction(this); } }
        public TimeSpan TimeRemaining { get; private set; } = TimeSpan.Zero;
        private DateTime StartTime { get; set; }

        public BackgroundAction(string name, int maxvalue) {
            if (maxvalue > 0) { 
                Name = name;
                MaxValue = maxvalue;
                id = idCount;
                idCount++;
                StartTime = DateTime.Now;
                AddAction(this);
            }
        }

        private void SetValue(int value) {
            if (value >= _maxValue) {
                RemoveAction(this);
            } else {
                TimeSpan tookTime = DateTime.Now - StartTime;
                TimeSpan perItem = TimeSpan.FromTicks(tookTime.Ticks / value);
                TimeRemaining = TimeSpan.FromTicks((perItem.Ticks * _maxValue) - tookTime.Ticks);
            }
        }

    }
}
