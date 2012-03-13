﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CryEngine.Utils;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace CryEngine
{
    public enum UIParameterType
    {
        Any = 0,
        Bool,
        Int,
        Float,
        String,
    };

    public enum UIEventDirection
    {
        UIToSystem = 0,
        SystemToUI,
    };

    public struct UIParameterDescription
    {
        public UIParameterType Type;
        public string Name;
        public string DisplayName;
        public string Description;
        public UIParameterDescription(UIParameterType type = UIParameterType.Any)
        {
            Type = UIParameterType.Any;
            Name = "Undefined";
            DisplayName = "Undefined";
            Description = "Undefined";
        }
        public UIParameterDescription(string name, string displayname, string description, UIParameterType type = UIParameterType.Any)
        {
            Type = type;
            Name = name;
            DisplayName = displayname;
            Description = description;
        }
    };

    public struct UIEventDescription
    {

        public UIParameterType Type;
        public string Name;
        public string DisplayName;
        public string Description;

        public Object[] Params;
        public bool IsDynamic;
        public string DynamicName;
        public string DynamicDesc;
        public UIEventDescription(UIParameterType type = UIParameterType.Any)
        {
            Type = UIParameterType.Any;
            Name = "Undefined";
            DisplayName = "Undefined";
            Description = "Undefined";

            IsDynamic = false;
            DynamicName = "Array";
            DynamicDesc = "";
            Params = null;
        }
        public UIEventDescription(string name, string displayname, string description, bool isdyn = false, string dynamicname = "Array", string dynamicdesc = "")
        {
            Type = UIParameterType.Any;
            Name = name;
            DisplayName = displayname;
            Description = description;

            IsDynamic = isdyn;
            DynamicName = dynamicname;
            DynamicDesc = dynamicdesc;
            Params = null;
        }
        public void SetDynamic(string dynamicname, string dynamicdesc){
            IsDynamic = true;
            DynamicName = dynamicname;
            DynamicDesc = dynamicdesc;
        }
    };

    public class UIEventArgs : System.EventArgs
    {
        public int Event;
        public Object[] Args;
        public UIEventArgs()
		{
            Event = 0;
		}
    }

	public class UI
	{
        [MethodImpl(MethodImplOptions.InternalCall)]
	    extern internal static int _RegisterEvent(string eventsystem, int direction, UIEventDescription desc);
        [MethodImpl(MethodImplOptions.InternalCall)]
	    extern internal static bool _RegisterToEventSystem(string eventsystem, int type);
	    [MethodImpl(MethodImplOptions.InternalCall)]
        extern internal static void _UnregisterFromEventSystem(string eventsystem, int type);
        [MethodImpl(MethodImplOptions.InternalCall)]
        extern internal static void _SendEvent(string eventsystem, int Event, object[] args);
        [MethodImpl(MethodImplOptions.InternalCall)]
        extern internal static void _SendNamedEvent(string eventsystem, string Event, object[] args);


        private static Dictionary<int, string> eventMap;

		public static void OnEvent(string EventSystem, string EventName, int EventID, object[] args)
		{
            UIEventArgs e = new UIEventArgs();
            e.Event = EventID;
            e.Args = args;
            Debug.LogAlways("Event: {0}.{1} = {2}", EventSystem, EventName, e.Event);
            int i, c;
            Object o;
            c = e.Args.Length;
            for (i = 0; i < c; i++){
                o = e.Args[i];
                Debug.LogAlways("Arg {0}/{1}: {2} {3}", i+1, c, o.GetType().Name, o);
            }
			SendEvent("MySystemEvent", "TestEvent2", new object[2] { EventName, EventID });

			//Events(null, new UIEventArgs());
		}


        public static int RegisterEvent(string eventsystem, UIEventDirection direction, UIEventDescription desc)
        {
            return _RegisterEvent(eventsystem, (int)direction, desc);
        }

        public static bool RegisterToEventSystem(string eventsystem, UIEventDirection direction)
        {
            return _RegisterToEventSystem(eventsystem, (int)direction);
        }
        public static void UnregisterFromEventSystem(string eventsystem, UIEventDirection direction)
        {
            _UnregisterFromEventSystem(eventsystem, (int)direction);
        }


        public static void SendEvent(string eventsystem, int Event, object[] args)
        {
            _SendEvent(eventsystem, Event, args);
        }
        public static void SendEvent(string eventsystem, string Event, object[] args)
        {
            _SendNamedEvent(eventsystem, Event, args);
        }

		public delegate void UIEventDelegate(object sender, UIEventArgs e);

		/// <summary>
		/// UI.Events += MyUIEventDelegateMethod;
		/// </summary>
		public static event UIEventDelegate Events;

        public static void TestInit()
        {
            bool b;
            b = RegisterToEventSystem("MenuEvents", UIEventDirection.UIToSystem);
            Debug.LogAlways("RegisterToEventSystem(\"MenuEvents\") == {0}", b);
            UIEventDescription desc = new UIEventDescription("TestEvent", "TestEventDName", "TestEventDescription");
            desc.Params = new Object[2];
            desc.Params[0] = new UIParameterDescription("Param1", "Param1DName", "Param1Desc", UIParameterType.String);
            desc.Params[1] = new UIParameterDescription("Param2", "Param2DName", "Param2Desc", UIParameterType.Int);
            int i = RegisterEvent("MyEvent", UIEventDirection.UIToSystem, desc);
            Debug.LogAlways("RegisterEvent == {0}", i);
            i = RegisterEvent("MyEvent2", UIEventDirection.UIToSystem, desc);
            Debug.LogAlways("RegisterEvent2 == {0}", i);

			desc = new UIEventDescription("BoidCount", "BoidCount", "Sets the boid count");
			desc.Params = new Object[1];
			desc.Params[0] = new UIParameterDescription("Count", "Count", "Number of available boids", UIParameterType.Int);
			i = RegisterEvent("AngryBoids", UIEventDirection.SystemToUI, desc);
        }
	}
}
