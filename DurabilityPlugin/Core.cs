using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Text;
using System.Timers;

using Sandbox.Common.ObjectBuilders;

using SEModAPIExtensions.API.Plugin;
using SEModAPIExtensions.API.Plugin.Events;

using SEModAPIInternal.API.Common;
using SEModAPIInternal.API.Entity;
using SEModAPIInternal.API.Entity.Sector.SectorObject;
using SEModAPIInternal.API.Entity.Sector.SectorObject.CubeGrid;
using SEModAPIInternal.API.Entity.Sector.SectorObject.CubeGrid.CubeBlock;
using SEModAPIInternal.Support;

using VRageMath;

namespace DurabilityPlugin
{
	public class Core : PluginBase
	{
		#region "Attributes"

		private bool m_isActive;
		private Thread m_mainUpdateLoop;

		private static float m_damageRate;
		private static float m_solarRadiationRate;
		private static float m_micrometeoriteRate;
		private static float m_wearAndTearRate;

		private static float m_reactorRadiationBase;
		private static float m_reactorRadiationBaseRange;
		private static float m_reactorPowerRate;

		protected TimeSpan m_timeSinceLastUpdate;
		protected DateTime m_lastUpdate;
		protected DateTime m_lastFullScan;

		#endregion

		#region "Constructors and Initializers"

		public Core()
		{
			m_isActive = false;

			m_lastUpdate = DateTime.Now;
			m_lastFullScan = DateTime.Now;
			m_timeSinceLastUpdate = DateTime.Now - m_lastUpdate;

			m_mainUpdateLoop = new Thread(MainUpdate);

			m_damageRate = 1.0f;
			m_solarRadiationRate = 0.125f;
			m_micrometeoriteRate = 0.4f;
			m_wearAndTearRate = 1.0f;

			m_reactorRadiationBase = 0.1f;
			m_reactorRadiationBaseRange = 5.0f;
			m_reactorPowerRate = 0.2f;
		}

		#endregion

		#region "Properties"

		[Category("Durability Plugin")]
		[Browsable(true)]
		[ReadOnly(false)]
		public float DamageRate
		{
			get { return m_damageRate; }
			set { m_damageRate = value; }
		}

		[Category("Durability Plugin")]
		[Browsable(true)]
		[ReadOnly(false)]
		public float SolarRadiationRate
		{
			get { return m_solarRadiationRate; }
			set { m_solarRadiationRate = value; }
		}

		[Category("Durability Plugin")]
		[Browsable(true)]
		[ReadOnly(false)]
		public float MicrometeoriteRate
		{
			get { return m_micrometeoriteRate; }
			set { m_micrometeoriteRate = value; }
		}

		[Category("Durability Plugin")]
		[Browsable(true)]
		[ReadOnly(false)]
		public float WearAndTearRate
		{
			get { return m_wearAndTearRate; }
			set { m_wearAndTearRate = value; }
		}

		[Category("Durability Plugin")]
		[Browsable(true)]
		[ReadOnly(false)]
		public float ReactorRadiationBase
		{
			get { return m_reactorRadiationBase; }
			set { m_reactorRadiationBase = value; }
		}

		[Category("Durability Plugin")]
		[Browsable(true)]
		[ReadOnly(false)]
		public float ReactorRangeBase
		{
			get { return m_reactorRadiationBaseRange; }
			set { m_reactorRadiationBaseRange = value; }
		}

		[Category("Durability Plugin")]
		[Browsable(true)]
		[ReadOnly(false)]
		public float ReactorPowerRate
		{
			get { return m_reactorPowerRate; }
			set { m_reactorPowerRate = value; }
		}

		#endregion

		#region "Methods"

		#region "EventHandlers"

		public override void Init()
		{
			m_isActive = true;
		}

		public override void Update()
		{
			if (!m_mainUpdateLoop.IsAlive)
			{
				m_mainUpdateLoop.Start();
			}
		}

		public override void Shutdown()
		{
			m_isActive = false;

			m_mainUpdateLoop.Interrupt();
		}

		#endregion

		protected void MainUpdate()
		{
			DateTime lastFullScan = DateTime.Now;
			DateTime lastMainLoop = DateTime.Now;
			TimeSpan timeSinceLastMainLoop = DateTime.Now - lastMainLoop;
			float averageMainLoopInterval = 0;
			float averageMainLoopTime = 0;
			DateTime lastProfilingMessage = DateTime.Now;
			TimeSpan timeSinceLastProfilingMessage = DateTime.Now - lastProfilingMessage;

			while (m_isActive)
			{
				try
				{
					DateTime mainLoopStart = DateTime.Now;

					TimeSpan timeSinceLastFullScan = DateTime.Now - m_lastFullScan;
					if (timeSinceLastFullScan.TotalSeconds > 15)
					{
						m_lastFullScan = DateTime.Now;

						FullScan();
					}

					//Performance profiling
					timeSinceLastMainLoop = DateTime.Now - lastMainLoop;
					lastMainLoop = DateTime.Now;
					TimeSpan mainLoopRunTime = DateTime.Now - mainLoopStart;
					averageMainLoopInterval = (averageMainLoopInterval + (float)timeSinceLastMainLoop.TotalMilliseconds) / 2;
					averageMainLoopTime = (averageMainLoopTime + (float)mainLoopRunTime.TotalMilliseconds) / 2;
					timeSinceLastProfilingMessage = DateTime.Now - lastProfilingMessage;
					if (timeSinceLastProfilingMessage.TotalSeconds > 10)
					{
						lastProfilingMessage = DateTime.Now;

						LogManager.APILog.WriteLine("DurabilityPlugin - Average main loop interval: " + Math.Round(averageMainLoopInterval, 2).ToString() + "ms");
						LogManager.APILog.WriteLine("DurabilityPlugin - Average main loop time: " + Math.Round(averageMainLoopTime, 2).ToString() + "ms");
					}

					//Pause between loops
					int nextSleepTime = Math.Min(500, Math.Max(100, 200 + (200 - (int)timeSinceLastMainLoop.TotalMilliseconds) / 2));
					Thread.Sleep(nextSleepTime);
				}
				catch (Exception ex)
				{
					LogManager.GameLog.WriteLine(ex);
					Thread.Sleep(5000);
				}
			}
		}

		private void FullScan()
		{
			try
			{
				LogManager.APILog.WriteLine("Damaging stuff!");

				DateTime startFullScan = DateTime.Now;
				List<CubeGridEntity> cubeGridList = SectorObjectManager.Instance.GetTypedInternalData<CubeGridEntity>();
				foreach (CubeGridEntity cubeGrid in cubeGridList)
				{
					if (cubeGrid == null || cubeGrid.IsDisposed)
						continue;

					DoDamage(cubeGrid);
				}
				TimeSpan totalFullScanTime = DateTime.Now - startFullScan;
				LogManager.APILog.WriteLine("Finished damaging stuff in " + Math.Round(totalFullScanTime.TotalSeconds, 4).ToString() + " seconds");
			}
			catch (Exception ex)
			{
				LogManager.GameLog.WriteLine(ex);
			}
		}

		private void DoDamage(CubeGridEntity cubeGrid)
		{
			if (cubeGrid == null)
				return;
			if (cubeGrid.IsDisposed)
				return;

			List<ReactorEntity> reactors = new List<ReactorEntity>();
			foreach (CubeBlockEntity cubeBlock in cubeGrid.CubeBlocks)
			{
				if (cubeBlock is ReactorEntity)
				{
					ReactorEntity reactor = (ReactorEntity)cubeBlock;
					if(reactor.Power <= 0)
						continue;

					reactors.Add((ReactorEntity)cubeBlock);
				}
			}

			foreach (CubeBlockEntity cubeBlock in cubeGrid.CubeBlocks)
			{
				if (cubeBlock.IntegrityPercent <= 0.05f)
					continue;

				Random random = new Random((int)DateTime.Now.ToBinary());

				float solarRadiationDamage = SolarRadiationRate;
				float randomDamage = (float)random.NextDouble() * MicrometeoriteRate;
				float wearAndTearDamange = 0.0f;
				float reactorRadiationDamage = 0.0f;

				//Calculate wear and tear damage based on power usage
				if (cubeBlock is FunctionalBlockEntity)
				{
					FunctionalBlockEntity functionalBlock = (FunctionalBlockEntity)cubeBlock;
					wearAndTearDamange = functionalBlock.CurrentInput * WearAndTearRate;
				}

				//Only calculate reactor radiation for non-reactors
				if (!(cubeBlock is ReactorEntity))
				{
					foreach (ReactorEntity reactor in reactors)
					{
						float blockSize = 0.5f;
						if (cubeGrid.GridSizeEnum == MyCubeSize.Large)
							blockSize = 2.5f;
						Vector3 blockDistance = ((Vector3I)cubeBlock.Min - (Vector3I)reactor.Min) * blockSize;
						float totalDistance = blockDistance.Length();

						float leakingRadiation = ReactorRadiationBase + (0.5f * (1.0f / reactor.IntegrityPercent) - 0.5f);
						float reactorRange = ReactorRangeBase + ReactorPowerRate * reactor.Power;
						if (totalDistance <= reactorRange)
							reactorRadiationDamage += (reactorRange / totalDistance) * leakingRadiation;
					}
				}

				float damage = (float)m_timeSinceLastUpdate.TotalHours * (solarRadiationDamage + randomDamage + wearAndTearDamange + reactorRadiationDamage) * DamageRate;

				cubeBlock.IntegrityPercent = (float)Math.Max(0.05, cubeBlock.IntegrityPercent - (damage / 100.0f));
			}
		}

		#endregion
	}
}
