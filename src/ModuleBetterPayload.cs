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

		//is delivered?
		[KSPField(isPersistant = true)]
		public bool isDelivered = false;

		// part direction which should see the main body
		// can be up, forward, right, -up, -forward, -right
		[KSPField(isPersistant = true)]
		public string direction = "up";

		//btsm integration
		public PartModule btsmComPayload;
		public BaseField btsmDeliverPayloadField;
		public BaseField btsmStatusField;
		//these two are for hiding btsm "deliver" button. 
		//Because btsm classes aren't public, i need to make some nasty tricks
		public BaseField btsmIsDeliveredField;
		public BaseField btsmIsInvalidatedField;

		//active these on delivery
		private List<ModuleDecouple> decouplers;
		private List<Animation> antennasAnims;

		//payload attitude control
		public State DeployementState = State.NotControlled;
		public double maxTimeToMove = 0;
		public Vector3d move = Vector3d.zero;
		public Vector3d moveTo = Vector3d.zero;

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
				Fields["deliverPayloadButton"].guiName = "error";
				Fields["deliverPayloadButton"].guiActive = false;
				Fields["deliverPayloadButton"].guiActiveEditor = false;
				Fields["deliverPayloadButton"].uiControlEditor.controlEnabled = false;
			}
			else
			{
				Fields["deliverPayloadButton"].guiActive = true;
			}

			antennasAnims = new List<Animation>();
			foreach (Animation animation in part.FindModelAnimators())
			{
				//stop animation (without the partmodule companion, the mts play its animation at startup)
				animation.Stop();
				antennasAnims.Add(animation);
			}

			decouplers = new List<ModuleDecouple>();
			foreach (PartModule pm in part.Modules)
			{
				if (pm.moduleName.Equals("BTSMModuleCommercialPayload"))
				{
					//get btsm field
					btsmComPayload = pm;
					btsmDeliverPayloadField = pm.Fields["deliverPayloadDepressed"];
					btsmStatusField = pm.Fields["payloadStatus"];
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

			if (isDelivered)
			{
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

		//TODO: use the btsm-one
		// using a KSPField instead of KSPEvent as fields can be active on uncontrollable vessels
		[KSPField(isPersistant = false, guiActive = true, guiActiveEditor = false, guiName = "state")]
		public string stateGui = "idle";


		public override void OnUpdate()
		{
			base.OnUpdate();
			//has btsm think it's ok for delivering?
			if (btsmDeliverPayloadField.guiActive)
			{
				//set the payload as "invalid" => let me take control of it.
				btsmIsDeliveredField.SetValue(true, btsmIsDeliveredField.host);
				btsmIsInvalidatedField.SetValue(true, btsmIsDeliveredField.host);
				btsmDeliverPayloadField.guiActive = false;
				Fields["deliverPayloadButton"].guiActive = true;
			}

			switch (this.DeployementState)
			{
				case State.KillVelocity:
				// kill velocity before moving to the target body
					if (part.rigidbody.angularVelocity.magnitude > 0.02)
					{
						//kill a part of the velocity
						part.rigidbody.AddTorque(-part.rigidbody.angularVelocity*2);
					}
					else
					{
						//velocity killed : init MoveToTaget
						this.DeployementState = State.MoveToTarget;
						this.moveTo = (vessel.mainBody.position - vessel.findWorldCenterOfMass()).normalized;
						this.move = Vector3d.Cross(getVesselUp(), moveTo).normalized;
						this.part.rigidbody.AddTorque(move * 0.5, ForceMode.VelocityChange);
						this.maxTimeToMove = vessel.missionTime + 6;
						if (!isDelivered) stateGui = "Targeting";
					}
					break;
				case State.MoveToTarget:
				// It's moving quickly to the right direction
					if (Vector3.Dot(getVesselUp(), moveTo) > 0.98)
					{
						//direction almost good : stop
						part.rigidbody.AddTorque(move * -0.5f, ForceMode.VelocityChange);
						move = Vector3d.zero;

						//init FinalApproch
						this.DeployementState = State.FinalApproch;
						this.moveTo = (vessel.mainBody.position - vessel.findWorldCenterOfMass()).normalized;
						this.move = Vector3d.Cross(getVesselUp(), moveTo).normalized;
						this.part.rigidbody.AddTorque(move * 0.1, ForceMode.VelocityChange);
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
						// can't face the body on time: someone push me => restart.
						this.DeployementState = State.KillVelocity;
					}
					break;
				case State.FinalApproch:
				// moving slowly to the exact direction
					if (Vector3.Dot(getVesselUp(), moveTo) > 0.995)
					{
						//right direction : stop rotation
						part.rigidbody.AddTorque(move * -0.1, ForceMode.VelocityChange);
						move = Vector3d.zero;
						this.DeployementState = State.InService;
					}
					else if (maxTimeToMove < vessel.missionTime)
					{
						// can't face the body on time: someone push me => restart.
						this.DeployementState = State.KillVelocity;
					}
					break;
				case State.InService:
				// facing the right direction : do not move
				//TODO: should turn slowly at the same time/rate we rotate the body.
					if (!isDelivered)
					{
						// first time right facing : deliver the payload (award  btsm contract)
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
						// we're not facing the body anymore ( pushed by other? normal rotation shift?)
						// => init FinalApproch for slow re-turn
						this.moveTo = (vessel.mainBody.position - vessel.findWorldCenterOfMass()).normalized;
						this.move = Vector3d.Cross(getVesselUp(), moveTo).normalized;
						this.part.rigidbody.AddTorque(move * 0.1, ForceMode.VelocityChange);
						this.maxTimeToMove = vessel.missionTime + 10;
						this.DeployementState = State.FinalApproch;
					}
					else if (part.rigidbody.angularVelocity.magnitude > 0.005)
					{
						//kill remaining rotation
						part.rigidbody.AddTorque(-part.rigidbody.angularVelocity);
					}
					break;
			}

			//push the deliver button?
			if (deliverPayloadButton)
			{
				deliverPayloadButton = false;
				deployPayload();
			}
		}

		//start the deploy sequence
		public void deployPayload()
		{
			this.DeployementState = State.KillVelocity;
			stateGui = "Stopping rotation";

			//remove button
			Fields["deliverPayloadButton"].guiActive = false;

			// remove autopilot => if decouple, not useful
			// vessel.Autopilot.sas.manualoverride(true);
			// vessel.autopilot.Disable();

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
