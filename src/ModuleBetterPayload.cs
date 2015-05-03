using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace BetterPayload
{
	public class ModuleBetterPayload : PartModule
	{
		//todo: like the stock contract

		//is delivered?
		[KSPField(isPersistant = true)]
		public bool isDelivered = false;

		[KSPField(isPersistant = true)]
		public string direction = "up";

		public PartModule btsmComPayload;
		public BaseField btsmDeliverPayloadField;
		//these two are for hiding btsm "deliver" button. 
		//Because btsm classes aren't public, i need to make some nasty tricks
		public BaseField btsmIsDeliveredField;
		public BaseField btsmIsInvalidatedField;

		public BaseField btsmStatusField;
		
		//List<ModuleCommand> moduleCommand;
		private List<ModuleDecouple> decouplers;

		private List<Animation> antennasAnims;

		public State DeployementState = State.NotControlled;
		private double maxTimeToMove = 0;
		private Vector3d move = Vector3d.zero;
		private Vector3d moveTo = Vector3d.zero;

		public enum State
		{
			NotControlled = 0,
			KillVelocity = 1,
			MoveToTarget = 2,
			FinalApproch = 3,
			InService = 4,
		}

		public override void OnStart(PartModule.StartState state)
		{
			base.OnStart(state);

			if (state == StartState.Editor)
			{
				//print("BetterPayload Start: disable!");
				Fields["deliverPayloadButton"].guiName = "error";
				Fields["deliverPayloadButton"].guiActive = false;
				Fields["deliverPayloadButton"].guiActiveEditor = false;
				Fields["deliverPayloadButton"].uiControlEditor.controlEnabled = false;
			}
			else
			{
				//print("BetterPayload Start: enable!");
				Fields["deliverPayloadButton"].guiActive = true;
			}

			antennasAnims = new List<Animation>();
			//int numAnim = 0;
			foreach (Animation animation in part.FindModelAnimators())
			{
				//print("BetterPayload Find animation : " + animation.name);
				//if (animation.name == "antenna")
				//{
				//	//animation.name = "antenna" + numAnim;
				//	antennasAnims.Add(animation);
				//	//numAnim++;
				//}
				animation.Stop();
				antennasAnims.Add(animation);

			}

			decouplers = new List<ModuleDecouple>();
			foreach (PartModule pm in part.Modules)
			{
				//print("BetterPayload pm " + pm.moduleName);
				if (pm.moduleName.Equals("BTSMModuleCommercialPayload"))
				{
					//print("BetterPayload FIND BTSMModuleCommercialPayload");
					btsmComPayload = pm;
					//print("BetterPayload FIND pm.Fields = " + pm.Fields.Count);
					for (int i = 0; i < pm.Fields.Count; i++)
					{
						//print("BetterPayload pm.Field " + pm.Fields[i].name);
					}
					btsmDeliverPayloadField = pm.Fields["deliverPayloadDepressed"];
					//print("BetterPayload btsmComPayloadField " + btsmDeliverPayloadField.guiActive);
					btsmStatusField = pm.Fields["payloadStatus"];
					//print("BetterPayload btsmStatusField " + btsmStatusField.guiActive);
					btsmIsDeliveredField = pm.Fields["isDelivered"];
					btsmIsInvalidatedField = pm.Fields["isInvalidated"];
				}
				else if (pm is ModuleDecouple)
				{
					ModuleDecouple decoupler = (ModuleDecouple)pm;
					decoupler.Events["Decouple"].guiActive = false;
					decoupler.Actions["DecoupleAction"].active = false;
					decouplers.Add(decoupler);
				}

			}

			//moduleCommand = new List<ModuleCommand>();
			//foreach (Part aPart in vessel.Parts)
			//{
			//	foreach (PartModule pm in aPart.Modules)
			//	{
			//		if (pm.moduleName.Equals("ModuleCommand"))
			//		{
			//			//print("BetterPayload ModuleCommand " + pm.part.name);
			//			moduleCommand.Add((ModuleCommand)pm);
			//		}
			//	}
			//}

			if (isDelivered)
			{
				//print("start: Delivered!");
				Fields["deliverPayloadButton"].guiActive = false;
				DeployementState = State.InService;
				stateGui = "Delivered";
				//deploy things
				int i = 0;
				foreach (Animation anim in antennasAnims)
				{
					anim.Play();
					i++;
				}
			}
			else
			{
				DeployementState = State.NotControlled;
				stateGui = "Not delivered";
			}

		}

		public Vector3 getVesselUp(){
			switch(direction){
				case "up":
					return part.vessel.transform.up;
				case "-up":
					return -part.vessel.transform.up;
				case "forward":
					return part.vessel.transform.forward;
				case "-forward":
					return -part.vessel.transform.forward;
				case "right":
					return part.vessel.transform.right;
				case "-right":
					return -part.vessel.transform.right;
			}
			return Vector3.zero;
		}

		// using a KSPField instead of KSPEvent as fields can be active on uncontrollable vessels
		[KSPField(isPersistant = false, guiActive = true, guiActiveEditor = false, guiName = "Deliver"), UI_Toggle(disabledText = "", enabledText = "")]
		public bool deliverPayloadButton = false;

		// using a KSPField instead of KSPEvent as fields can be active on uncontrollable vessels
		[KSPField(isPersistant = false, guiActive = true, guiActiveEditor = false, guiName = "state")]
		public string stateGui = "idle";


		public override void OnUpdate()
		{
			base.OnUpdate();
			//has btsm think it's ok for delivering?
			if (btsmDeliverPayloadField.guiActive)
			{
				//print("BetterPayload btsmDeliverPayloadField ACTIVE!!!!");
				//set the payload as "invalide" => let me take control of it.
				btsmIsDeliveredField.SetValue(true, btsmIsDeliveredField.host);
				btsmIsInvalidatedField.SetValue(true, btsmIsDeliveredField.host);
				btsmDeliverPayloadField.guiActive = false;
				Fields["deliverPayloadButton"].guiActive = true;
			}
			//{
			//	int i = 0;
			//	foreach (Animation anim in antennasAnims)
			//	{
			//		if (i == 0)
			//		{
			//			//print("anim: " + anim.isPlaying + ", " + anim.IsPlaying(anim.name) +", "
			//				+ anim.enabled);
			//		}
			//		anim.Play();
			//		i++;
			//	}
			//}

			switch (this.DeployementState)
			{
				case State.KillVelocity:
					if (part.rigidbody.angularVelocity.magnitude > 0.02)
					{
						//print("BetterPayload KIllvelocity");
						part.rigidbody.AddTorque(-part.rigidbody.angularVelocity*2);
					}
					else
					{
						//print("BetterPayload velocity killed");
						this.DeployementState = State.MoveToTarget;
						this.moveTo = (vessel.mainBody.position - vessel.findWorldCenterOfMass()).normalized;
						this.move = Vector3d.Cross(getVesselUp(), moveTo).normalized;
						this.part.rigidbody.AddTorque(move * 0.5, ForceMode.VelocityChange);
						//print("BetterPayload move to from " + vessel.missionTime);
						this.maxTimeToMove = vessel.missionTime + 6;
						if (!isDelivered) stateGui = "Targeting";
					}
					break;
				case State.MoveToTarget:
					//print("BetterPayload MoveToTarget " + vessel.missionTime + " =>" + maxTimeToMove);
					if (Vector3.Dot(getVesselUp(), moveTo) > 0.98)
					{
						//print("BetterPayload MoveToTarget ok " + Vector3.Dot(getVesselUp(), moveTo));
						part.rigidbody.AddTorque(move * -0.5f, ForceMode.VelocityChange);
						move = Vector3d.zero;

						this.DeployementState = State.FinalApproch;
						this.moveTo = (vessel.mainBody.position - vessel.findWorldCenterOfMass()).normalized;
						this.move = Vector3d.Cross(getVesselUp(), moveTo).normalized;
						this.part.rigidbody.AddTorque(move * 0.1, ForceMode.VelocityChange);
						//print("BetterPayload move to from " + vessel.missionTime);
						this.maxTimeToMove = vessel.missionTime + 3;

						if (!isDelivered) stateGui = "Deploying";

						//deploy antennas (if not already done)
						if (!isDelivered)
						{
							int i = 0;
							foreach (Animation anim in antennasAnims)
							{
								anim.Play();
								i++;
							}
						}
					}
					else if (maxTimeToMove < vessel.missionTime)
					{
						//print("BetterPayload Fail to move, retry");
						this.DeployementState = State.KillVelocity;
					}
					break;
				case State.FinalApproch:
					//print("BetterPayload FinalApproch at " + vessel.missionTime + " =>" + maxTimeToMove);
					if (Vector3.Dot(getVesselUp(), moveTo) > 0.995)
					{
						//print("BetterPayload FinalApproch end! " + Vector3.Dot(getVesselUp(), moveTo));
						part.rigidbody.AddTorque(move * -0.1, ForceMode.VelocityChange);
						move = Vector3d.zero;
						this.DeployementState = State.InService;
					}
					else if (maxTimeToMove < vessel.missionTime)
					{
						//print("BetterPayload Fail FinalApproch to move, retry");
						this.DeployementState = State.KillVelocity;
					}
					break;
				case State.InService:
					if (!isDelivered)
					{
						//print("BetterPayload drifted! deliver!");
						//set "ok"
						if (btsmDeliverPayloadField != null)
						{
							//re-put the payload in "valid" state
							btsmIsDeliveredField.SetValue(false, btsmIsDeliveredField.host);
							btsmIsInvalidatedField.SetValue(false, btsmIsDeliveredField.host);
							// look, it's delivered!
							btsmDeliverPayloadField.SetValue(true, btsmDeliverPayloadField.host);
						}
						isDelivered = true;
						stateGui = "Delivered";
						Fields["deliverPayloadButton"].guiActive = false;
					}
					if (Vector3.Dot(getVesselUp(), moveTo) < 0.98)
					{
						//print("BetterPayload drifted! re-move");
						this.moveTo = (vessel.mainBody.position - vessel.findWorldCenterOfMass()).normalized;
						this.move = Vector3d.Cross(getVesselUp(), moveTo).normalized;
						this.part.rigidbody.AddTorque(move * 0.1, ForceMode.VelocityChange);
						//print("BetterPayload move to from " + vessel.missionTime);
						this.maxTimeToMove = vessel.missionTime + 10;
						this.DeployementState = State.FinalApproch;
					}
					else if (part.rigidbody.angularVelocity.magnitude > 0.005)
					{
						//print("BetterPayload drift correction: "+part.rigidbody.angularVelocity.magnitude);
						part.rigidbody.AddTorque(-part.rigidbody.angularVelocity);
					}
					break;
			}

			if (deliverPayloadButton)
			{
				deliverPayloadButton = false;
				deployPayload();
			}
		}

		//start the deploy sequence
		public void deployPayload()
		{
			//print("BetterPayload : deploy called " + direction);
			this.DeployementState = State.KillVelocity;
			stateGui = "Stopping rotation";

			//remove button
			Fields["deliverPayloadButton"].guiActive = false;

			//print("BetterPayload Autopilot " + vessel.Autopilot.SAS + "," + vessel.Autopilot.RSAS);
			//vessel.Autopilot.RSAS.UnsetTarget();
			//vessel.Autopilot.RSAS.Terminate();
			//vessel.Autopilot.SAS.DisconnectFlyByWire();
			vessel.Autopilot.SAS.ManualOverride(true);
			//vessel.Autopilot.SAS.Destroy();
			vessel.Autopilot.Disable();
			//vessel.Autopilot.SetupModules();
			//vessel.Autopilot.Update();
			//print("BetterPayload Autopilot " + vessel.Autopilot.SAS + "," + vessel.Autopilot.RSAS);

			//make vessel incrontrolable => if decouple, not useful
			//foreach (ModuleCommand mc in moduleCommand)
			//{
			//	mc.minimumCrew = 666;
			//}

			if (!isDelivered)
			{
				foreach (ModuleDecouple decoupler in decouplers)
				{
					decoupler.Events["Decouple"].Invoke();
				}
			}
		}

	}
}
