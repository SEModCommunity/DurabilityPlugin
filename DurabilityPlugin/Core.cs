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
		private static float m_damageRate;

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

			m_damageRate = 1.0f;
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

		#endregion

		#region "Methods"

		#region "EventHandlers"

		public override void Init()
		{
			m_isActive = true;
		}

		public override void Update()
		{
			if (!m_isActive)
				return;

			m_timeSinceLastUpdate = DateTime.Now - m_lastUpdate;
			m_lastUpdate = DateTime.Now;

			TimeSpan timeSinceLastFullScan = DateTime.Now - m_lastFullScan;
			if (timeSinceLastFullScan.TotalMilliseconds > 10000)
			{
				m_lastFullScan = DateTime.Now;

				Thread thread = new Thread(FullScan);
				thread.Start();
			}
		}

		public override void Shutdown()
		{
			m_isActive = false;
		}

		#endregion

		private void FullScan()
		{
			try
			{
				LogManager.APILog.WriteLineAndConsole("Damaging stuff!");

				DateTime startFullScan = DateTime.Now;
				List<CubeGridEntity> cubeGridList = SectorObjectManager.Instance.GetTypedInternalData<CubeGridEntity>();
				foreach (CubeGridEntity cubeGrid in cubeGridList)
				{
					if (cubeGrid == null || cubeGrid.IsDisposed)
						continue;

					DoDamage(cubeGrid);
				}
				TimeSpan totalFullScanTime = DateTime.Now - startFullScan;
				LogManager.APILog.WriteLineAndConsole("Finished damaging stuff in " + Math.Round(totalFullScanTime.TotalSeconds, 4).ToString() + " seconds");
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

				float solarRadiationDamage = 0.125f;
				float randomDamage = (float)random.NextDouble() * 0.4f;
				float wearAndTearDamange = 0.0f;	//TODO - Determine this amount based on power usage
				float reactorRadiationDamage = 0.0f;

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

						float leakingRadiation = 0.1f + (0.5f * (1.0f / reactor.IntegrityPercent) - 0.5f);
						float reactorRange = 5.0f + 0.2f * reactor.Power;
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
