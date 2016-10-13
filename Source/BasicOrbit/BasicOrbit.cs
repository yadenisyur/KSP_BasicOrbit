﻿#region License
/*
 * Basic Orbit
 * 
 * BasicOrbit - Primary MonoBehaviour for controlling the addon
 * 
 * Copyright (C) 2016 DMagic
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by 
 * the Free Software Foundation, either version 3 of the License, or 
 * (at your option) any later version. 
 * 
 * This program is distributed in the hope that it will be useful, 
 * but WITHOUT ANY WARRANTY; without even the implied warranty of 
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the 
 * GNU General Public License for more details. 
 * 
 * You should have received a copy of the GNU General Public License 
 * along with this program.  If not, see <http://www.gnu.org/licenses/>. 
 * 
 * 
 */
#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BasicOrbit.Modules.OrbitModules;
using BasicOrbit.Modules.TargetModules;
using BasicOrbit.Unity.Unity;
using BasicOrbit.Unity.Interface;
using UnityEngine;

namespace BasicOrbit
{
	[KSPAddon(KSPAddon.Startup.Flight, false)]
    public class BasicOrbit : MonoBehaviour, IBasicOrbit
    {
		private BasicHUD orbitHUD;
		private BasicHUD targetHUD;

		private Apoapsis apo;
		private Periapsis peri;
		private Inclination inc;
		private Eccentricity ecc;
		private Period period;
		private SemiMajorAxis SMA;
		private LongAscending LAN;
		private ArgOfPeriapsis AoPE;
		private OrbitAltitude altitude;
		private RadarAltitude radar;
		private TerrainAltitude terrain;
		private Location location;

		private ClosestApproach closest;
		private RelVelocityAtClosest closestVel;
		private DistanceToTarget distance;
		private RelInclination relInc;
		private RelVelocity relVel;
		private AngleToPrograde angToPro;

		private BasicSettings settings;

		private static BasicOrbit instance = null;

		private BasicOrbit_Panel orbitPanel;
		private BasicOrbit_Panel targetPanel;

		private BasicOrbitAppLauncher appLauncher;
		private string _version;

		public static BasicOrbit Instance
		{
			get { return instance; }
		}

		private void Awake()
		{
			if (instance != null)
				Destroy(gameObject);

			instance = this;

			settings = BasicSettings.Instance;
		}

		private void Start()
		{
			orbitHUD = new BasicHUD(AddOrbitModules());
			orbitHUD.Position = settings.orbitPosition;

			targetHUD = new BasicHUD(AddTargetModules());
			targetHUD.Position = settings.targetPosition;

			Assembly assembly = AssemblyLoader.loadedAssemblies.GetByAssembly(Assembly.GetExecutingAssembly()).assembly;
			var ainfoV = Attribute.GetCustomAttribute(assembly, typeof(AssemblyInformationalVersionAttribute)) as AssemblyInformationalVersionAttribute;
			switch (ainfoV == null)
			{
				case true: _version = ""; break;
				default: _version = ainfoV.InformationalVersion; break;
			}

			if (settings.showOrbitPanel)
				AddOrbitPanel();

			if (settings.showTargetPanel)
				AddTargetPanel();

			appLauncher = gameObject.AddComponent<BasicOrbitAppLauncher>();
		}

		private void OnDestroy()
		{
			instance = null;

			apo.IsActive = false;
			peri.IsActive = false;
			inc.IsActive = false;
			ecc.IsActive = false;
			LAN.IsActive = false;
			period.IsActive = false;
			radar.IsActive = false;
			terrain.IsActive = false;

			closest.IsActive = false;
			distance.IsActive = false;
			relInc.IsActive = false;
			relVel.IsActive = false;

			if (orbitPanel != null)
				Destroy(orbitPanel.gameObject);

			if (targetPanel != null)
				Destroy(targetPanel.gameObject);

			settings.orbitPosition = orbitHUD.Position;
			settings.targetPosition = targetHUD.Position;

			if (appLauncher != null)
				Destroy(appLauncher);

			if (settings.Save())
				BasicOrbit.BasicLogging("Settings file saved");
		}
		
		private void Update()
		{
			if (!FlightGlobals.ready)
				return;

			if (orbitHUD == null || targetHUD == null)
				return;

			Vessel v = FlightGlobals.ActiveVessel;

			if (v == null)
				return;

			if (orbitHUD.IsVisible)
			{
				switch (v.situation)
				{
					case Vessel.Situations.LANDED:
					case Vessel.Situations.PRELAUNCH:
						apo.IsActive = apo.AlwaysShow;
						peri.IsActive = peri.AlwaysShow;
						inc.IsActive = inc.AlwaysShow;
						ecc.IsActive = ecc.AlwaysShow;
						LAN.IsActive = LAN.AlwaysShow;
						AoPE.IsActive = AoPE.AlwaysShow;
						SMA.IsActive = SMA.AlwaysShow;
						period.IsActive = period.AlwaysShow;
						altitude.IsActive = altitude.AlwaysShow;
						radar.IsActive = radar.AlwaysShow;
						location.IsActive = true;
						terrain.IsActive = true;
						break;
					case Vessel.Situations.SPLASHED:
						apo.IsActive = apo.AlwaysShow;
						peri.IsActive = peri.AlwaysShow;
						inc.IsActive = inc.AlwaysShow;
						ecc.IsActive = ecc.AlwaysShow;
						LAN.IsActive = LAN.AlwaysShow;
						AoPE.IsActive = AoPE.AlwaysShow;
						SMA.IsActive = SMA.AlwaysShow;
						period.IsActive = period.AlwaysShow;
						altitude.IsActive = altitude.AlwaysShow;
						location.IsActive = true;
						radar.IsActive = true;
						terrain.IsActive = true;
						break;
					case Vessel.Situations.FLYING:
						apo.IsActive = apo.AlwaysShow || v.orbit.eccentricity < 1;
						radar.IsActive = true;
						terrain.IsActive = true;
						location.IsActive = true;
						inc.IsActive = inc.AlwaysShow || v.altitude > v.mainBody.scienceValues.flyingAltitudeThreshold / 3;
						altitude.IsActive = altitude.AlwaysShow;
						LAN.IsActive = LAN.AlwaysShow;
						AoPE.IsActive = AoPE.AlwaysShow;
						SMA.IsActive = SMA.AlwaysShow;
						ecc.IsActive = ecc.AlwaysShow;
						peri.IsActive = v.orbit.PeA > 0;
						period.IsActive = period.AlwaysShow;
						break;
					case Vessel.Situations.SUB_ORBITAL:
						apo.IsActive = apo.AlwaysShow || v.orbit.eccentricity < 1;
						radar.IsActive = true;
						inc.IsActive = true;
						ecc.IsActive = true;
						location.IsActive = true;

						if (v.orbit.PeA < 0)
							peri.IsActive = peri.AlwaysShow || Math.Abs(v.orbit.PeA) < v.mainBody.Radius / 5 || (v.orbit.eccentricity >= 1 && v.orbit.timeToPe > 0);
						else
							peri.IsActive = peri.AlwaysShow || v.orbit.eccentricity < 1 || v.orbit.timeToPe > 0;
						
						altitude.IsActive = altitude.AlwaysShow;
						LAN.IsActive = LAN.AlwaysShow;
						AoPE.IsActive = AoPE.AlwaysShow;
						SMA.IsActive = SMA.AlwaysShow;
						period.IsActive = period.AlwaysShow;
						terrain.IsActive = terrain.AlwaysShow;
						break;
					default:
						apo.IsActive = apo.AlwaysShow || v.orbit.eccentricity < 1;
						peri.IsActive = peri.AlwaysShow || v.orbit.eccentricity < 1 || v.orbit.timeToPe > 0;
						inc.IsActive = true;
						ecc.IsActive = true;
						LAN.IsActive = true;
						AoPE.IsActive = true;
						SMA.IsActive = true;
						period.IsActive = period.AlwaysShow || v.orbit.eccentricity < 1;
						altitude.IsActive = true;

						location.IsActive = location.AlwaysShow;
						radar.IsActive = radar.AlwaysShow;
						terrain.IsActive = terrain.AlwaysShow;
						break;
				}
			}

			if (targetHUD.IsVisible)
			{
				if (!BasicTargetting.TargetValid())
				{
					closest.IsActive = false;
					distance.IsActive = false;
					relInc.IsActive = false;
					relVel.IsActive = false;
					angToPro.IsActive = false;
					closestVel.IsActive = false;

					BasicTargetting.UpdateOn = false;
				}
				else
				{
					switch (v.situation)
					{
						case Vessel.Situations.LANDED:
						case Vessel.Situations.PRELAUNCH:
						case Vessel.Situations.SPLASHED:
							angToPro.IsActive = angToPro.AlwaysShow && (BasicTargetting.IsCelestial && FlightGlobals.currentMainBody.referenceBody != null && FlightGlobals.currentMainBody.referenceBody != FlightGlobals.currentMainBody);
							closest.IsActive = closest.AlwaysShow && ((BasicTargetting.IsCelestial && (closest.CachedBody || BasicTargetting.BodyIntersect)) || (BasicTargetting.IsVessel && (closestVel.Cached || BasicTargetting.VesselIntersect)));
							closestVel.IsActive = closestVel.AlwaysShow && (BasicTargetting.IsVessel && (closestVel.Cached || BasicTargetting.VesselIntersect));
							distance.IsActive = BasicTargetting.IsCelestial || BasicTargetting.IsVessel;
							relVel.IsActive = relVel.AlwaysShow && (BasicTargetting.IsCelestial || BasicTargetting.IsVessel);
							relInc.IsActive = relInc.AlwaysShow && (BasicTargetting.IsCelestial || BasicTargetting.IsVessel);
							break;
						case Vessel.Situations.FLYING:
							angToPro.IsActive = angToPro.AlwaysShow && (BasicTargetting.IsCelestial && FlightGlobals.currentMainBody.referenceBody != null && FlightGlobals.currentMainBody.referenceBody != FlightGlobals.currentMainBody);
							closest.IsActive = ((BasicTargetting.IsCelestial && (closest.CachedBody || BasicTargetting.BodyIntersect)) || (BasicTargetting.IsVessel && closest.CachedVessel)) && (closest.AlwaysShow || v.altitude > v.mainBody.scienceValues.flyingAltitudeThreshold);
							closestVel.IsActive = (BasicTargetting.IsVessel && (closestVel.Cached || BasicTargetting.VesselIntersect)) && (closestVel.AlwaysShow || v.altitude > v.mainBody.scienceValues.flyingAltitudeThreshold);
							distance.IsActive = BasicTargetting.IsCelestial || BasicTargetting.IsVessel;
							relVel.IsActive = BasicTargetting.IsCelestial || BasicTargetting.IsVessel;
							relInc.IsActive = (BasicTargetting.IsCelestial || BasicTargetting.IsVessel) && (relInc.AlwaysShow || v.altitude > v.mainBody.scienceValues.flyingAltitudeThreshold / 3);
							break;
						default:
							angToPro.IsActive = BasicTargetting.IsCelestial && FlightGlobals.currentMainBody.referenceBody != null && FlightGlobals.currentMainBody.referenceBody != FlightGlobals.currentMainBody;
							closest.IsActive = (BasicTargetting.IsCelestial && (closest.CachedBody || BasicTargetting.BodyIntersect)) || (BasicTargetting.IsVessel && (closestVel.Cached || BasicTargetting.VesselIntersect));
							closestVel.IsActive = BasicTargetting.IsVessel && (closestVel.Cached || BasicTargetting.VesselIntersect);
							distance.IsActive = BasicTargetting.IsCelestial || BasicTargetting.IsVessel;
							relVel.IsActive = BasicTargetting.IsCelestial || BasicTargetting.IsVessel;
							relInc.IsActive = BasicTargetting.IsCelestial || BasicTargetting.IsVessel;
							break;
					}

					BasicTargetting.UpdateOn = true;
				}
			}
			else
				BasicTargetting.UpdateOn = false;

			if (BasicTargetting.UpdateOn)
				BasicTargetting.Update();
		}

		public string Version
		{
			get { return _version; }
		}

		public bool ShowOrbit
		{
			get { return settings.showOrbitPanel; }
			set
			{
				settings.showOrbitPanel = value;

				if (value)
					AddOrbitPanel();
				else
					CloseOrbit();
			}
		}

		public bool ShowTarget
		{
			get { return settings.showTargetPanel; }
			set
			{
				settings.showTargetPanel = value;

				if (value)
					AddTargetPanel();
				else
					CloseTarget();
			}
		}

		public float Alpha
		{
			get { return BasicSettings.Instance.panelAlpha; }
			set
			{
				BasicSettings.Instance.panelAlpha = value;

				SetPanelAlpha(value);
			}
		}

		public float Scale
		{
			get { return BasicSettings.Instance.UIScale; }
			set
			{
				BasicSettings.Instance.UIScale = value;

				SetPanelScale(value);
			}
		}

		public float MasterScale
		{
			get { return GameSettings.UI_SCALE; }
		}

		public BasicOrbit_Panel GetOrbit
		{
			get { return orbitPanel; }
		}

		public BasicOrbit_Panel GetTarget
		{
			get { return targetPanel; }
		}

		public IBasicPanel GetOrbitPanel
		{
			get { return orbitHUD; }
		}

		public IBasicPanel GetTargetPanel
		{
			get { return targetHUD; }
		}

		private List<IBasicModule> AddOrbitModules()
		{
			List<IBasicModule> modules = new List<IBasicModule>();

			apo = new Apoapsis("Apoapsis");
			peri = new Periapsis("Periapsis");
			inc = new Inclination("Inclination");
			ecc = new Eccentricity("Eccentricity");
			period = new Period("Period");
			SMA = new SemiMajorAxis("Semi Major Axis");
			LAN = new LongAscending("LAN");
			AoPE = new ArgOfPeriapsis("Arg of Pe");
			altitude = new OrbitAltitude("Altitude");
			radar =new RadarAltitude("Radar Altitude");
			terrain = new TerrainAltitude("Terrain Altitude");
			location = new Location("Location");

			apo.IsVisible = settings.showApoapsis;
			apo.AlwaysShow = settings.showApoapsisAlways;
			peri.IsVisible = settings.showPeriapsis;
			peri.AlwaysShow = settings.showPeriapsisAlways;
			inc.IsVisible = settings.showInclination;
			inc.AlwaysShow = settings.showInclinationAlways;
			ecc.IsVisible = settings.showEccentricity;
			ecc.AlwaysShow = settings.showEccentricityAlways;
			period.IsVisible = settings.showPeriod;
			period.AlwaysShow = settings.showPeriodAlways;
			SMA.IsVisible = settings.showSMA;
			SMA.AlwaysShow = settings.showSMAAlways;
			LAN.IsVisible = settings.showLAN;
			LAN.AlwaysShow = settings.showLANAlways;
			AoPE.IsVisible = settings.showAoPe;
			AoPE.AlwaysShow = settings.showAoPeAlways;
			altitude.IsVisible = settings.showOrbitAltitude;
			altitude.AlwaysShow = settings.showOrbitAltitudeAlways;
			radar.IsVisible = settings.showRadar;
			radar.AlwaysShow = settings.showRadarAlways;
			terrain.IsVisible = settings.showTerrain;
			terrain.AlwaysShow = settings.showTerrainAlways;
			location.IsVisible = settings.showLocation;
			location.AlwaysShow = settings.showLocationAlways;

			modules.Add(AoPE);
			modules.Add(LAN);
			modules.Add(SMA);
			modules.Add(terrain);
			modules.Add(radar);
			modules.Add(altitude);
			modules.Add(location);
			modules.Add(period);
			modules.Add(ecc);
			modules.Add(inc);
			modules.Add(peri);
			modules.Add(apo);

			return modules;
		}

		private List<IBasicModule> AddTargetModules()
		{
			List<IBasicModule> modules = new List<IBasicModule>();

			closest = new ClosestApproach("Closest Approach");
			closestVel = new RelVelocityAtClosest("Rel Vel At Appr");
			distance = new DistanceToTarget("Dist To Target");
			relInc = new RelInclination("Rel Inclination");
			relVel = new RelVelocity("Rel Velocity");
			angToPro = new AngleToPrograde("Ang To Prograde");

			closest.IsVisible = settings.showClosestApproach;
			closest.AlwaysShow = settings.showClosestApproachAlways;
			closestVel.IsVisible = settings.showClosestApproachVelocity;
			closestVel.AlwaysShow = settings.showClosestApproachVelocityAlways;
			distance.IsVisible = settings.showDistance;
			distance.AlwaysShow = settings.showDistanceAlways;
			relInc.IsVisible = settings.showRelInclination;
			relInc.AlwaysShow = settings.showRelInclinationAlways;
			relVel.IsVisible = settings.showRelVelocity;
			relVel.AlwaysShow = settings.showRelVelocityAlways;
			angToPro.IsVisible = settings.showAngleToPrograde;
			angToPro.AlwaysShow = settings.showAngleToProgradeAlways;

			modules.Add(relVel);
			modules.Add(relInc);
			modules.Add(angToPro);
			modules.Add(closestVel);
			modules.Add(closest);
			modules.Add(distance);

			return modules;
		}

		private IEnumerator PanelStartup()
		{
			while (!FlightGlobals.ready || FlightGlobals.ActiveVessel == null)
				yield return null;

			if (settings.showOrbitPanel)
				AddOrbitPanel();

			if (settings.showTargetPanel)
				AddTargetPanel();
		}

		private void AddOrbitPanel()
		{
			if (orbitPanel != null)
				return;

			if (BasicOrbitLoader.PanelPrefab == null)
				return;

			if (orbitHUD == null)
				return;

			GameObject obj = Instantiate(BasicOrbitLoader.PanelPrefab);

			if (obj == null)
				return;

			obj.transform.SetParent(MainCanvasUtil.MainCanvas.transform, false);

			orbitPanel = obj.GetComponent<BasicOrbit_Panel>();

			if (orbitPanel == null)
				return;

			orbitPanel.setPanel(orbitHUD);

			orbitHUD.IsVisible = true;
		}

		private void CloseOrbit()
		{
			if (orbitPanel == null)
				return;

			if (orbitHUD != null)
				orbitHUD.IsVisible = false;

			orbitPanel.Close();

			orbitPanel = null;
		}

		private void AddTargetPanel()
		{
			if (targetPanel != null)
				return;

			if (BasicOrbitLoader.PanelPrefab == null)
				return;

			if (targetHUD == null)
				return;

			GameObject obj = Instantiate(BasicOrbitLoader.PanelPrefab);

			if (obj == null)
				return;

			obj.transform.SetParent(MainCanvasUtil.MainCanvas.transform, false);

			targetPanel = obj.GetComponent<BasicOrbit_Panel>();

			if (targetPanel == null)
				return;

			targetPanel.setPanel(targetHUD);

			targetHUD.IsVisible = true;
		}

		private void CloseTarget()
		{
			if (targetPanel == null)
				return;

			if (targetHUD != null)
				targetHUD.IsVisible = false;

			targetPanel.Close();

			targetPanel = null;
		}

		private void SetPanelScale(float scale)
		{
			Vector3 old = new Vector3(1, 1, 1);

			if (targetPanel != null)
				targetPanel.transform.localScale = old * scale;

			if (orbitPanel != null)
				orbitPanel.transform.localScale = old * scale;
		}

		private void SetPanelAlpha(float alpha)
		{
			if (targetPanel != null)
			{
				targetPanel.SetAlpha(alpha);
				targetPanel.SetOldAlpha();
			}

			if (orbitPanel != null)
			{
				orbitPanel.SetAlpha(alpha);
				orbitPanel.SetOldAlpha();
			}
		}

		public static void BasicLogging(string s, params object[] m)
		{
			Debug.Log(string.Format("[Basic Orbit] " + s, m));
		}

    }
}
