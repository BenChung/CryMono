﻿using System;
using System.Collections.Generic;
using CryEngine;
using CryEngine.Initialization;
using CryEngine.Native;
using Moq;
using NUnit.Framework;
using System.Linq;

namespace CryBrary.Tests
{
    [Serializable]
	public abstract class CryBraryTests
	{
        [NonSerialized]
	    protected List<Mock> _mocks;

        protected Mock<T> GetMock<T>() where T : class
        {
            return _mocks.First(m => m.Object is T) as Mock<T>;
        }
        

		[SetUp]
		public void Init()
		{
            // Don't show gui's when unit testing
            CryEngine.Utils.Settings.IsGuiSupported = false;

		    InitializeMocks();
		}

        protected virtual void ConfigureMocks()
        {
            var logMock = GetMock<INativeLoggingMethods>();
            Action<string> loggingMethod = System.Console.WriteLine;

            logMock.Setup(m => m.Log(It.IsAny<string>())).Callback(loggingMethod);
            logMock.Setup(m => m.LogAlways(It.IsAny<string>())).Callback(loggingMethod);
            logMock.Setup(m => m.Warning(It.IsAny<string>())).Callback(loggingMethod);
        }

        protected void InitializeMocks()
        {
            _mocks = new List<Mock>();

            // Gotta mock em all
            var actorMock = new Mock<INativeActorMethods>();
            _mocks.Add(actorMock);
            NativeMethods.Actor = actorMock.Object;

            var cvarMock = new Mock<INativeCVarMethods>();
            _mocks.Add(cvarMock);
            NativeMethods.CVar = cvarMock.Object;

            var debugMock = new Mock<INativeDebugMethods>();
            _mocks.Add(debugMock);
            NativeMethods.Debug = debugMock.Object;

            var engine3DMock = new Mock<INative3DEngineMethods>();
            _mocks.Add(engine3DMock);
            NativeMethods.Engine3D = engine3DMock.Object;

            var entityMock = new Mock<INativeEntityMethods>();
            _mocks.Add(entityMock);
            NativeMethods.Entity = entityMock.Object;

            var flowNodeMock = new Mock<INativeFlowNodeMethods>();
            _mocks.Add(flowNodeMock);
            NativeMethods.FlowNode = flowNodeMock.Object;

            var gameRulesMock = new Mock<INativeGameRulesMethods>();
            _mocks.Add(gameRulesMock);
            NativeMethods.GameRules = gameRulesMock.Object;

            var inputMock = new Mock<INativeInputMethods>();
            _mocks.Add(inputMock);
            NativeMethods.Input = inputMock.Object;

            var itemSystemMock = new Mock<INativeItemSystemMethods>();
            _mocks.Add(itemSystemMock);
            NativeMethods.ItemSystem = itemSystemMock.Object;

            var levelMock = new Mock<INativeLevelMethods>();
            _mocks.Add(levelMock);
            NativeMethods.Level = levelMock.Object;

            var materialMock = new Mock<INativeMaterialMethods>();
            _mocks.Add(materialMock);
            NativeMethods.Material = materialMock.Object;

            var particleMock = new Mock<INativeParticleEffectMethods>();
            _mocks.Add(particleMock);
            NativeMethods.Particle = particleMock.Object;

            var physicsMock = new Mock<INativePhysicsMethods>();
            _mocks.Add(physicsMock);
            NativeMethods.Physics = physicsMock.Object;

            var renderedMock = new Mock<INativeRendererMethods>();
            _mocks.Add(renderedMock);
            NativeMethods.Renderer = renderedMock.Object;

            var scriptTableMock = new Mock<INativeScriptTableMethods>();
            _mocks.Add(scriptTableMock);
            NativeMethods.ScriptTable = scriptTableMock.Object;

            var timeMock = new Mock<INativeTimeMethods>();
            _mocks.Add(timeMock);
            NativeMethods.Time = timeMock.Object;

            var uiMock = new Mock<INativeUIMethods>();
            _mocks.Add(uiMock);
            NativeMethods.UI = uiMock.Object;

			var viewMock = new Mock<INativeViewMethods>();
			_mocks.Add(viewMock);
			NativeMethods.View = viewMock.Object;

			var networkMock = new Mock<INativeNetworkMethods>();
			_mocks.Add(networkMock);
			NativeMethods.Network = networkMock.Object;

            var logMock = new Mock<INativeLoggingMethods>();
            _mocks.Add(logMock);
            NativeMethods.Log = logMock.Object;

            var appDomainMock = new Mock<INativeAppDomainMethods>();
            _mocks.Add(appDomainMock);
            NativeMethods.AppDomain = appDomainMock.Object;

            ConfigureMocks();
        }
    }
}