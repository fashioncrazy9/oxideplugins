// Reference: RustBuild
using System.Collections.Generic;
using System;
using System.Reflection;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Plugins;


namespace Oxide.Plugins
{
    [Info("Prod", "Reneb", "2.2.0", ResourceId = 683)]
    class Prod : RustPlugin
    {

        private int prodAuth;
        private string helpProd;
        private string noAccess;
        private string noTargetfound;
        private string noCupboardPlayers;
        private string Toolcupboard;
        private string noBlockOwnerfound;
        private string noCodeAccess;
        private string codeLockList;
        private string boxNeedsCode;
        private string boxCode;

        private FieldInfo serverinput;
        private FieldInfo codelockwhitelist;
        private FieldInfo codenum;
        private FieldInfo npcnextTick;
        private FieldInfo meshinstances;


        private Vector3 eyesAdjust;
        private bool Changed;

        [PluginReference]
        Plugin DeadPlayersList;

        [PluginReference]
        Plugin BuildingOwners;

        void Loaded()
        {
            LoadVariables();
            eyesAdjust = new Vector3(0f, 1.5f, 0f);
            serverinput = typeof(BasePlayer).GetField("serverInput", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
            codelockwhitelist = typeof(CodeLock).GetField("whitelistPlayers", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
            codenum = typeof(CodeLock).GetField("code", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
            npcnextTick = typeof(NPCAI).GetField("nextTick", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
            meshinstances = typeof(MeshColliderBatch).GetField("instances", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
        }

        private object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                Changed = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                Changed = true;
            }
            return value;
        }

        private bool isPluginDev;
        private bool dumpAll;
        private void LoadVariables()
        {
            prodAuth = Convert.ToInt32(GetConfig("Prod", "authLevel", 1));
            isPluginDev = Convert.ToBoolean(GetConfig("Plugin Dev", "Are you are plugin dev?", false));
            dumpAll = Convert.ToBoolean(GetConfig("Plugin Dev", "Dump all components of all entities that you are looking at? (false will do only the closest one)", false));
            helpProd = Convert.ToString(GetConfig("Messages", "helpProd", "/prod on a building or tool cupboard to know who owns it."));
            noAccess = Convert.ToString(GetConfig("Messages", "noAccess", "You don't have access to this command"));
            noTargetfound = Convert.ToString(GetConfig("Messages", "noTargetfound", "You must look at a tool cupboard or building"));
            noCupboardPlayers = Convert.ToString(GetConfig("Messages", "noCupboardPlayers", "No players has access to this cupboard"));
            Toolcupboard = Convert.ToString(GetConfig("Messages", "Toolcupboard", "Tool Cupboard"));
            noBlockOwnerfound = Convert.ToString(GetConfig("Messages", "noBlockOwnerfound", "No owner found for this building block"));
            noCodeAccess = Convert.ToString(GetConfig("Messages", "noCodeAccess", "No players has access to this Lock"));
            codeLockList = Convert.ToString(GetConfig("Messages", "codeLockList", "CodeLock whitelist:"));
            boxNeedsCode = Convert.ToString(GetConfig("Messages", "boxNeedsCode", "Can't find owners of an item without a Code Lock"));
            boxCode = Convert.ToString(GetConfig("Messages", "Code", "Code is: {0}"));
            
            if (Changed)
            {
                SaveConfig();
                Changed = false;
            }
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Prod: Creating a new config file");
            Config.Clear();
            LoadVariables();
        }

        private bool hasAccess(BasePlayer player)
        {
            if (player.net.connection.authLevel < prodAuth)
                return false;
            return true;
        }
        [ChatCommand("prod")]
        void cmdChatProd(BasePlayer player, string command, string[] args)
        {
            if (!(hasAccess(player)))
            {
                SendReply(player, noAccess);
                return;
            }
            var input = serverinput.GetValue(player) as InputState;
            var currentRot = Quaternion.Euler(input.current.aimAngles) * Vector3.forward;
            var target = DoRay(player.transform.position + eyesAdjust, currentRot);
            if (target is bool)
            {
                SendReply(player, noTargetfound);
                return;
            }
            /*
            if (((Collider)target).GetComponent<MeshColliderBatch>() != null)
            {
                MeshColliderBatch meshbatch = ((Collider)target).GetComponent<MeshColliderBatch>();
                Debug.Log(meshbatch.Count.ToString());
                Debug.Log(meshbatch.BatchedCount.ToString());
                ListDictionary<Component, ColliderCombineInstance> meshinstance = meshinstances.GetValue(meshbatch) as ListDictionary<Component, ColliderCombineInstance>;
                foreach(ColliderCombineInstance pair in meshinstance.Values)
                {
                    Debug.Log(pair.collider.ToString());
                    Dump(pair.collider);
                }
            }*/
            if (target is BuildingBlock)
            {
                GetBuildingblockOwner(player, (BuildingBlock)target);
            }
            else if (target is BuildingPrivlidge)
            {
                GetToolCupboardUsers(player, (BuildingPrivlidge)target);
            }
            else if (target is SleepingBag)
            {
                GetDeployedItemOwner(player, (SleepingBag)target);
            }
            else if (target is StorageContainer)
            {
                GetStorageBoxCode(player, (StorageContainer)target);
            }
            if(isPluginDev && !dumpAll)
            {
                Dump(target as Collider);
            }
        } 
        private void GetStorageBoxCode(BasePlayer player, StorageContainer box)
        {
            if (box.HasSlot(BaseEntity.Slot.Lock))
            {
                BaseLock thelock = box.GetSlot(BaseEntity.Slot.Lock) as BaseLock;
                if (thelock as CodeLock)
                {
                    List<ulong> whitelisted = codelockwhitelist.GetValue(thelock as CodeLock) as List<ulong>;
                    string codevalue = codenum.GetValue(thelock as CodeLock) as string;
                    SendReply(player, string.Format(boxCode,codevalue));
                    SendReply(player, codeLockList);
                    if (whitelisted.Count == 0)
                    {
                        SendReply(player, noCodeAccess);
                        return;
                    }
                    foreach (ulong userid in whitelisted)
                    {
                        SendBasePlayerFind(player, userid);
                    }
                    return;
                }
            }
            SendReply(player, boxNeedsCode);
        }
        private void GetDeployedItemOwner(BasePlayer player, SleepingBag ditem)
        {
            SendReply(player, string.Format("Sleeping Bag '{0}': {1} - {2}", ditem.niceName.ToString(), FindPlayerName(ditem.deployerUserID), ditem.deployerUserID.ToString()));
        }
        private object FindOwnerBlock(BuildingBlock block)
        {
            object returnhook = BuildingOwners?.Call("FindBlockData", block);

            if (returnhook != null)
            {
                if (!(returnhook is bool))
                {
                    ulong ownerid = Convert.ToUInt64(returnhook);
                    return ownerid;
                }
            }
            Puts("Prod: To be able to obtain the owner of a building you need to install the BuildingOwner plugin.");
            return false;
        }

        private string FindPlayerName(ulong userId)
        {
            BasePlayer player = BasePlayer.FindByID(userId);
            if (player)
                return player.displayName + " (Online)";

            player = BasePlayer.FindSleeping(userId);
            if (player)
                return player.displayName + " (Sleeping)";

            string name = DeadPlayersList?.Call("GetPlayerName", userId) as string;
            if (name != null)
                return name + " (Dead)";

            return "Unknown player";
        }
        private void SendBasePlayerFind(BasePlayer player, ulong ownerid)
        {
            SendReply(player, string.Format("{0} {1}", FindPlayerName(ownerid), ownerid.ToString()));
        }
        private void GetBuildingblockOwner(BasePlayer player, BuildingBlock block)
        {
            if(block.GetComponent<Door>() != null)
            {
                if(block.HasSlot(BaseEntity.Slot.Lock))
                {
                    BaseEntity slotent = block.GetSlot(BaseEntity.Slot.Lock);
                    if(slotent != null)
                    {
                        CodeLock codelock = slotent.GetComponent<CodeLock>();
                        if(codelock != null)
                        {
                            List<ulong> whitelisted = codelockwhitelist.GetValue(codelock) as List<ulong>;
                            string codevalue = codenum.GetValue(codelock) as string;
                            SendReply(player, string.Format(boxCode, codevalue));
                            SendReply(player, codeLockList);
                            if (whitelisted.Count == 0)
                            {
                                SendReply(player, noCodeAccess);
                                return;
                            }
                            foreach (ulong userid in whitelisted)
                            {
                                SendBasePlayerFind(player, userid);
                            }
                        }
                    }
                }
            }

            object findownerblock = FindOwnerBlock(block);
            if (findownerblock is bool)
            {
                SendReply(player, noBlockOwnerfound);
                return;
            }
            ulong ownerid = (UInt64)findownerblock;
            SendBasePlayerFind(player, ownerid);
        }
        private void GetToolCupboardUsers(BasePlayer player, BuildingPrivlidge cupboard)
        {
            SendReply(player, string.Format("{0} - {1} {2} {3}", Toolcupboard, Math.Round(cupboard.transform.position.x).ToString(), Math.Round(cupboard.transform.position.y).ToString(), Math.Round(cupboard.transform.position.z).ToString()));
            if (cupboard.authorizedPlayers.Count == 0)
            {
                SendReply(player, noCupboardPlayers);
                return;
            }
            foreach (ProtoBuf.PlayerNameID pnid in cupboard.authorizedPlayers)
            {
                SendReply(player, string.Format("{0} - {1}", pnid.username.ToString(), pnid.userid.ToString()));
            }
        }
        private void Dump(Collider col)
        {
            Debug.Log("==================================================");
            Debug.Log(col.ToString() + " " + LayerMask.LayerToName(col.gameObject.layer).ToString());
            Debug.Log("========= NORMAL ===========");
            foreach(UnityEngine.Component com in col.GetComponents(typeof(UnityEngine.Component)) )
            {
                Debug.Log(com.ToString());
            }
            Debug.Log("========= PARENT ===========");
            foreach (UnityEngine.Component com in col.GetComponentsInParent(typeof(UnityEngine.Component)))
            {
                Debug.Log(com.ToString());
            }
            Debug.Log("========= CHILDREN ===========");
            foreach (UnityEngine.Component com in col.GetComponentsInChildren(typeof(UnityEngine.Component)))
            {
                Debug.Log(com.ToString());
            }
        }
        private object DoRay(Vector3 Pos, Vector3 Aim)
        {
            var hits = UnityEngine.Physics.RaycastAll(Pos, Aim);
            float distance = 100000f;
            object target = false;
            foreach (var hit in hits)
            {
                if (hit.collider != null)
                    Dump(hit.collider);
                if (hit.distance < distance)
                {
                    distance = hit.distance;
                    target = hit.GetEntity();
                }
            }
            return target;
        }

        void SendHelpText(BasePlayer player)
        {
            if (hasAccess(player))
                SendReply(player, helpProd);
        }
    }
}