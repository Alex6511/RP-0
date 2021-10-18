using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KerbalConstructionTime
{
    public class VesselBuildValidator
    {
        public bool CheckFacilityRequirements { get; set; } = true;
        public bool CheckPartAvailability { get; set; } = true;
        public bool CheckAvailableFunds { get; set; } = true;
        public Action<BuildListVessel> SuccessAction { get; set; }
        public Action FailureAction { get; set; }

        public void ProcessVessel(BuildListVessel blv)
        {
            SuccessAction = SuccessAction ?? ((_) => { });
            FailureAction = FailureAction ?? (() => { });

            if (!Utilities.CurrentGameIsCareer())
            {
                SuccessAction(blv);
                return;
            }

            ProcessFacilityChecks(blv, (BuildListVessel blv2) =>
                ProcessFundsChecks(blv2, (BuildListVessel blv3) =>
                    ProcessPartAvailability(blv3, (BuildListVessel blv4) =>
                        ProcessFundsChecks(blv4, SuccessAction))));
        }

        private void ProcessFacilityChecks(BuildListVessel blv, Action<BuildListVessel> successAction)
        {
            if (CheckFacilityRequirements)
            {
                //Check if vessel fails facility checks but can still be built
                List<string> facilityChecks = blv.MeetsFacilityRequirements(true);
                if (facilityChecks.Count != 0)
                {
                    PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), "editorChecksFailedPopup",
                        "Failed editor checks!",
                        "Warning! This vessel did not pass the editor checks! It will still be built, but you will not be able to launch it without upgrading. Listed below are the failed checks:\n"
                        + string.Join("\n", facilityChecks.Select(s => $"• {s}").ToArray()),
                        "Acknowledged",
                        false,
                        HighLogic.UISkin);

                    FailureAction();
                    return;
                }
            }

            successAction(blv);
        }

        private void ProcessPartAvailability(BuildListVessel blv, Action<BuildListVessel> successAction)
        {
            if (!CheckPartAvailability)
            {
                successAction(blv);
                return;
            }

            //Check if vessel contains locked or experimental parts, and therefore cannot be built
            Dictionary<AvailablePart, int> lockedParts = blv.GetLockedParts();
            if (lockedParts?.Count > 0)
            {
                KCTDebug.Log($"Tried to add {blv.ShipName} to build list but it contains locked parts.");

                //Simple ScreenMessage since there's not much you can do other than removing the locked parts manually.
                string lockedMsg = Utilities.ConstructLockedPartsWarning(lockedParts);
                var msg = new ScreenMessage(lockedMsg, 4f, ScreenMessageStyle.UPPER_CENTER);
                ScreenMessages.PostScreenMessage(msg);

                FailureAction();
                return;
            }

            Dictionary<AvailablePart, int> devParts = blv.GetExperimentalParts();
            if (devParts.Count == 0)
            {
                successAction(blv);
                return;
            }

            List<AvailablePart> unlockableParts = devParts.Keys.Where(p => ResearchAndDevelopment.GetTechnologyState(p.TechRequired) == RDTech.State.Available).ToList();
            int n = unlockableParts.Count();
            if (n > 0)
            {
                //PopupDialog asking you if you want to pay the entry cost for all the parts that can be unlocked (tech node researched)
                int unlockCost = Utilities.FindUnlockCost(unlockableParts);
                string mode = KCTGameStates.EditorShipEditingMode ? "save edits" : "build vessel";
                var buttons = new DialogGUIButton[] {
                    new DialogGUIButton("Acknowledged", () => { }),
                    new DialogGUIButton($"Unlock {n} part{(n > 1? "s":"")} for {unlockCost} Fund{(unlockCost > 1? "s":"")} and {mode}", () =>
                    {
                        if (Funding.Instance.Funds > unlockCost)
                        {
                            Utilities.UnlockExperimentalParts(unlockableParts);
                            successAction(blv);
                            return;
                        }
                        else
                        {
                            var msg = new ScreenMessage("Insufficient funds to unlock parts", 5f, ScreenMessageStyle.UPPER_CENTER);
                            ScreenMessages.PostScreenMessage(msg);
                            FailureAction();
                            return;
                        }
                    })
                };
            }
            else
            {
                string devMsg = Utilities.ConstructExperimentalPartsWarning(devParts);

                var buttons = new DialogGUIButton[] {
                    new DialogGUIButton("Acknowledged", () => { })
                };

                PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new MultiOptionDialog("devPartsCheckFailedPopup",
                        devMsg,
                        "Vessel cannot be built!",
                        HighLogic.UISkin,
                        buttons),
                    false,
                    HighLogic.UISkin);

                FailureAction();
            }
        }

        private void ProcessFundsChecks(BuildListVessel blv, Action<BuildListVessel> successAction)
        {
            if (CheckAvailableFunds)
            {
                double totalCost = blv.GetTotalCost();
                double prevFunds = Funding.Instance.Funds;
                if (totalCost > prevFunds)
                {
                    KCTDebug.Log($"Tried to add {blv.ShipName} to build list but not enough funds.");
                    KCTDebug.Log($"Vessel cost: {Utilities.GetTotalVesselCost(blv.ShipNode)}, Current funds: {prevFunds}");
                    var msg = new ScreenMessage("Not Enough Funds To Build!", 4f, ScreenMessageStyle.UPPER_CENTER);
                    ScreenMessages.PostScreenMessage(msg);

                    FailureAction();
                    return;
                }
            }

            successAction(blv);
        }
    }
}
