using System;
using System.Collections.Generic;

using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.Components;
using Sandbox.ModAPI;
using Ingame = Sandbox.ModAPI.Ingame;
using VRage.Components;
using VRage.ObjectBuilders;

using GardenConquest.Records;
using GardenConquest.Core;

namespace GardenConquest.Blocks {

	/// <summary>
	/// Helper methods for Classifier Blocks
	/// </summary>
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Beacon), 
        "GCUnlicensedHullClassifier", "GCWorkerHullClassifier", "GCFoundryHullClassifier",
        "GCScoutHullClassifier", "GCFighterHullClassifier", "GCGunshipHullClassifier", 
        "GCCorvetteHullClassifier", "GCFrigateHullClassifier", "GCDestroyerHullClassifier", 
        "GCCruiserHullClassifier", "GCHeavyCruiserHullClassifier", "GCBattleshipHullClassifier", 
        "GCOutpostHullClassifier", "GCInstallationHullClassifier", "GCFortressHullClassifier")]
    public class HullClassifier : MyGameLogicComponent {

		#region Static

		//private const String SHARED_SUBTYPE = "HullClassifier";
		public readonly static String[] SUBTYPES_IN_CLASS_ORDER = {
			"Unclassified",
			"Unlicensed",
			"Worker", "Foundry",
			"Scout", "Fighter", "Gunship",
			"Corvette",
			"Frigate",
			"Destroyer", "Cruiser",
			"HeavyCruiser", "Battleship",
			"Outpost", "Installation", "Fortress"
		};
		private static Logger s_Logger;

		/// <summary>
		/// If we recognize the subtype as belonging to a specific classifier, return its CLASS
		/// Otherwise, return Unclassified
		/// </summary>
		private static HullClass.CLASS HullClassFromSubTypeString(String subTypeString) {
			int longestMatchIndex = -1;
			String subtype;
			for (int i = 0; i < SUBTYPES_IN_CLASS_ORDER.Length; i++) {
				subtype = SUBTYPES_IN_CLASS_ORDER[i];
				if (subTypeString.Contains(subtype)) {
					if (longestMatchIndex == -1) {
						longestMatchIndex = i;
					} else if (subtype.Length > SUBTYPES_IN_CLASS_ORDER[longestMatchIndex].Length) {
							longestMatchIndex = i;
					}
				}
			}

			if (longestMatchIndex > -1) {
				return (HullClass.CLASS)longestMatchIndex;
			}

			// subtype not recognized, this shouldn't happen
			log("Classifier Subtype not recognized, defaulting to Unclassified", 
				"IDFromSubTypeString", Logger.severity.ERROR);
			return HullClass.CLASS.UNCLASSIFIED;
		}

        /*
		/// <summary>
		/// If we recognize the block's subtype as belonging to a classifier, return true
		/// </summary>
		public static bool isClassifierBlock(IMySlimBlock block) {
			IMyCubeBlock fatblock = block.FatBlock;
			if (fatblock != null && fatblock is Ingame.IMyBeacon) {
				String subTypeName = fatblock.BlockDefinition.SubtypeName;
				if (subTypeName.Contains(SHARED_SUBTYPE))
					return true;
			}

			return false;
		}
        */


		private static void log(String message, String method = null, 
			Logger.severity level = Logger.severity.DEBUG) {
			if (s_Logger == null)
				s_Logger = new Logger("Static", "HullClassifier");

			s_Logger.log(level, method, message);
		}

		#endregion
		#region instance

        private String m_SubTypeName;
        private GridEnforcer m_Enforcer;
        private IMyCubeBlock m_CubeBlock;
        private IMySlimBlock m_SlimBlock;

        public HullClass.CLASS HullClass { get; private set; }

        /*
		public HullClassifier(IMySlimBlock block, GridEnforcer ge) {

		}
         * */

		#endregion
        #region Component Hooks

        public override void Init(MyObjectBuilder_EntityBase objectBuilder) {
            base.Init(objectBuilder);

            m_SlimBlock = Container.Entity as IMySlimBlock;
            m_SubtypeName = FatBlock.BlockDefinition.SubtypeName;
            HullClass = HullClassFromSubTypeString(m_SubTypeName);
            Enforcer = ge;


            m_MergeBlock = Container.Entity as InGame.IMyShipMergeBlock;
            //m_Grid = m_MergeBlock.CubeGrid as IMyCubeGrid;

            m_Logger = new Logger(m_MergeBlock.EntityId.ToString(), "MergeBlock");
            log("Attached to merge block", "Init");

            (m_MergeBlock as IMyShipMergeBlock).BeforeMerge += beforeMerge;
        }

        public override void Close() {
            log("Merge block closing", "Close");
            (m_MergeBlock as IMyShipMergeBlock).BeforeMerge -= beforeMerge;
        }

        public override void Init(MyObjectBuilder_EntityBase objectBuilder) {
            base.Init(objectBuilder);
            m_Grid = Container.Entity as IMyCubeGrid;

            m_Logger = new Logger(m_Grid.EntityId.ToString(), "GridEnforcer");
            log("Loaded into new grid", "Init");

            // If this is not the server we don't need this class.
            // When we modify the grid on the server the changes should be
            // sent to all clients
            try {
                m_IsServer = Utility.isServer();
                log("Is server: " + m_IsServer, "Init");
                if (!m_IsServer) {
                    // No cleverness allowed :[
                    log("Disabled.  Not server.", "Init");
                    m_Logger = null;
                    m_Grid = null;
                    return;
                }
            }
            catch (NullReferenceException e) {
                log("Exception.  Multiplayer is not initialized.  Assuming server for time being: " + e,
                    "Init");
                // If we get an exception because Multiplayer was null (WHY KEEN???)
                // assume we are the server for a little while and check again later
                m_IsServer = true;
                m_CheckServerLater = true;
            }

            // We need to only turn on our rule checking after startup. Otherwise, if
            // a beacon is destroyed and then the server restarts, all but the first
            // 25 blocks will be deleted on startup.
            m_Grid.NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;

            m_BlockCount = 0;
            m_BlockTypeCounts = new int[s_Settings.BlockTypes.Length];
            m_Owner = new GridOwner(this);
            m_Classifiers = new Dictionary<long, HullClassifier>();
            m_Projectors = new Dictionary<long, InGame.IMyProjector>();

            setReservedToDefault();
            setEffectiveToDefault();
            //log("setClassification" + m_IsServer, "Init");
            m_Owner.setClassification(m_EffectiveClass);
            //log("end setClassification" + m_IsServer, "Init");

            m_Grid.OnBlockAdded += blockAdded;
            m_Grid.OnBlockRemoved += blockRemoved;
            m_Grid.OnBlockOwnershipChanged += blockOwnerChanged;
            m_GridSubscribed = true;
        }
    }
}
