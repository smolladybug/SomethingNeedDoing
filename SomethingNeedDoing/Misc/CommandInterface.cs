﻿using System;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;

using Dalamud.Game.ClientState.Conditions;
using Dalamud.Logging;
using ECommons.DalamudServices;
using ECommons.MathHelpers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using SomethingNeedDoing.Exceptions;
using SomethingNeedDoing.Interface;
using SomethingNeedDoing.IPC;

namespace SomethingNeedDoing.Misc;

/// <summary>
/// Miscellaneous functions that commands/scripts can use.
/// </summary>
public unsafe class CommandInterface : ICommandInterface
{
    private readonly AbandonDuty abandonDuty = Marshal.GetDelegateForFunctionPointer<AbandonDuty>(Service.SigScanner.ScanText("E8 ?? ?? ?? ?? 48 8B 43 28 B1 01"));

    private delegate void AbandonDuty(bool a1);

    private readonly byte* trueWeather = (byte*)(*(IntPtr*)Service.SigScanner.GetStaticAddressFromSig("48 8B 05 ?? ?? ?? ?? 48 83 C1 10 48 89 74 24") + 0x26);

    /// <inheritdoc/>
    public unsafe int GetWeatherId()
    {
        return (int)*this.trueWeather;
    }

    /// <inheritdoc/>
    public unsafe string GetPlayerPosition()
    {
        var pos = Service.ClientState.LocalPlayer?.Position ?? new(0, 0, 0);
        return string.Format("{0,3} {1,3} {2,3}", pos.X, pos.Y, pos.Z);
    }

    /// <inheritdoc/>
    public uint GetDiademCharges()
    {
        var addon = (AtkUnitBase*)Svc.GameGui.GetAddonByName("HWDAetherGauge");
        if (addon == null || !addon->IsVisible) return 0;

        uint fullCharges = 0;
        uint[] gauges = { 3, 4, 5, 6, 7 };

        foreach (var gaugeIndex in gauges)
        {
            var gaugeNode = addon->UldManager.NodeList[gaugeIndex];
            if (gaugeNode == null) continue;

            var chargeNode = gaugeNode->GetAsAtkComponentNode()->Component->UldManager.NodeList[3];
            if (chargeNode == null) continue;

            if (chargeNode->Width == gaugeNode->Width)
            {
                fullCharges++;
            }
        }

        return fullCharges;

    }

    /// <inheritdoc/>
    public string GetTargetName()
    {
        var target = Service.TargetManager.Target;
        if (target != null)
        {
            return target.Name.ToString();
        }
        return string.Empty;
    }

    /// <inheritdoc/>
    public string GetTargetId()
    {
        var target = Service.TargetManager.Target;
        if (target != null)
        {
            if (target.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.BattleNpc)
            {
                return target.DataId.ToString();
            }
            return target.Position.ToVector2().ToString();
        }
        return "";
    }

    /// <inheritdoc/>
    public string GetClosestGatheringNodeId()
    {
        var target = Service.ObjectTable
            .OrderBy(o => Vector3.Distance(o.Position, Service.ClientState.LocalPlayer!.Position))
            .Where(o => o.IsTargetable && o.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.GatheringPoint)
            .First();

        if (target == null)
        {
            return "false";
        }
        else
        {
            return target.Position.ToVector2().ToString();
        }
    }

    /// <summary>
    /// Gets the static instance.
    /// </summary>
    internal static CommandInterface Instance { get; } = new();

    /// <inheritdoc/>
    public bool IsCrafting()
        => Service.Condition[ConditionFlag.Crafting] && !Service.Condition[ConditionFlag.PreparingToCraft];

    /// <inheritdoc/>
    public bool IsNotCrafting()
        => !this.IsCrafting();

    /// <inheritdoc/>
    public unsafe bool IsCollectable()
    {
        var addon = this.GetSynthesisAddon();
        return addon->AtkUnitBase.UldManager.NodeList[34]->IsVisible;
    }

    /// <inheritdoc/>
    public unsafe string GetCondition(bool lower = true)
    {
        var addon = this.GetSynthesisAddon();
        var text = addon->Condition->NodeText.ToString();

        if (lower)
            text = text.ToLowerInvariant();

        return text;
    }

    /// <inheritdoc/>
    public bool HasCondition(string condition, bool lower = true)
    {
        var actual = this.GetCondition(lower);
        return condition == actual;
    }

    /// <inheritdoc/>
    public unsafe int GetProgress()
    {
        var addon = this.GetSynthesisAddon();
        return this.GetNodeTextAsInt(addon->CurrentProgress, "Could not parse current progress number in the Synthesis addon");
    }

    /// <inheritdoc/>
    public unsafe int GetMaxProgress()
    {
        var addon = this.GetSynthesisAddon();
        return this.GetNodeTextAsInt(addon->MaxProgress, "Could not parse max progress number in the Synthesis addon");
    }

    /// <inheritdoc/>
    public bool HasMaxProgress()
    {
        var current = this.GetProgress();
        var max = this.GetMaxProgress();
        return current == max;
    }

    /// <inheritdoc/>
    public unsafe int GetQuality()
    {
        var addon = this.GetSynthesisAddon();
        return this.GetNodeTextAsInt(addon->CurrentQuality, "Could not parse current quality number in the Synthesis addon");
    }

    /// <inheritdoc/>
    public unsafe int GetMaxQuality()
    {
        var addon = this.GetSynthesisAddon();
        return this.GetNodeTextAsInt(addon->MaxQuality, "Could not parse max quality number in the Synthesis addon");
    }

    /// <inheritdoc/>
    public bool HasMaxQuality()
    {
        var step = this.GetStep();

        if (step <= 1)
            return false;

        if (this.IsCollectable())
        {
            var current = this.GetQuality();
            var max = this.GetMaxQuality();
            return current == max;
        }
        else
        {
            var percentHq = this.GetPercentHQ();
            return percentHq == 100;
        }
    }

    /// <inheritdoc/>
    public unsafe int GetDurability()
    {
        var addon = this.GetSynthesisAddon();
        return this.GetNodeTextAsInt(addon->CurrentDurability, "Could not parse current durability number in the Synthesis addon");
    }

    /// <inheritdoc/>
    public unsafe int GetMaxDurability()
    {
        var addon = this.GetSynthesisAddon();
        return this.GetNodeTextAsInt(addon->StartingDurability, "Could not parse max durability number in the Synthesis addon");
    }

    /// <inheritdoc/>
    public int GetCp()
    {
        var cp = Service.ClientState.LocalPlayer?.CurrentCp ?? 0;
        return (int)cp;
    }

    /// <inheritdoc/>
    public int GetMaxCp()
    {
        var cp = Service.ClientState.LocalPlayer?.MaxCp ?? 0;
        return (int)cp;
    }

    /// <inheritdoc/>
    public int GetGp()
    {
        var gp = Service.ClientState.LocalPlayer?.CurrentGp ?? 0;
        return (int)gp;
    }

    /// <inheritdoc/>
    public int GetMaxGp()
    {
        var gp = Service.ClientState.LocalPlayer?.MaxGp ?? 0;
        return (int)gp;
    }

    /// <inheritdoc/>
    public unsafe int GetStep()
    {
        var addon = this.GetSynthesisAddon();
        var step = this.GetNodeTextAsInt(addon->StepNumber, "Could not parse current step number in the Synthesis addon");
        return step;
    }

    /// <inheritdoc/>
    public unsafe int GetPercentHQ()
    {
        var addon = this.GetSynthesisAddon();
        var step = this.GetNodeTextAsInt(addon->HQPercentage, "Could not parse percent hq number in the Synthesis addon");
        return step;
    }

    /// <inheritdoc/>
    public unsafe bool NeedsRepair(float below = 0)
    {
        var im = InventoryManager.Instance();
        if (im == null)
        {
            Service.Log.Error("InventoryManager was null");
            return false;
        }

        var equipped = im->GetInventoryContainer(InventoryType.EquippedItems);
        if (equipped == null)
        {
            Service.Log.Error("InventoryContainer was null");
            return false;
        }

        if (equipped->Loaded == 0)
        {
            Service.Log.Error($"InventoryContainer is not loaded");
            return false;
        }

        for (var i = 0; i < equipped->Size; i++)
        {
            var item = equipped->GetInventorySlot(i);
            if (item == null)
                continue;

            var itemCondition = Convert.ToInt32(Convert.ToDouble(item->Condition) / 30000.0 * 100.0);

            if (itemCondition <= below)
                return true;
        }

        return false;
    }

    /// <inheritdoc/>
    public unsafe bool CanExtractMateria(float within = 100)
    {
        var im = InventoryManager.Instance();
        if (im == null)
        {
            Service.Log.Error("InventoryManager was null");
            return false;
        }

        var equipped = im->GetInventoryContainer(InventoryType.EquippedItems);
        if (equipped == null)
        {
            Service.Log.Error("InventoryContainer was null");
            return false;
        }

        if (equipped->Loaded == 0)
        {
            Service.Log.Error("InventoryContainer is not loaded");
            return false;
        }

        var nextHighest = 0f;
        var canExtract = false;
        var allExtract = true;
        for (var i = 0; i < equipped->Size; i++)
        {
            var item = equipped->GetInventorySlot(i);
            if (item == null)
                continue;

            var spiritbond = item->Spiritbond / 100;
            if (spiritbond == 100f)
            {
                canExtract = true;
            }
            else
            {
                allExtract = false;
                nextHighest = Math.Max(spiritbond, nextHighest);
            }
        }

        if (allExtract)
        {
            Service.Log.Debug("All items are spiritbound, pausing");
            return true;
        }

        if (canExtract)
        {
            // Don't wait, extract immediately
            if (within == 100)
            {
                Service.Log.Debug("An item is spiritbound, pausing");
                return true;
            }

            // Keep going if the next highest spiritbonded item is within the allowed range
            // i.e. 100 and 99, do another craft to finish the 99.
            if (nextHighest >= within)
            {
                Service.Log.Debug($"The next highest spiritbond is above ({nextHighest} >= {within}), keep going");
                return false;
            }
            else
            {
                Service.Log.Debug($"The next highest spiritbond is below ({nextHighest} < {within}), pausing");
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc/>
    public unsafe bool HasStats(uint craftsmanship, uint control, uint cp)
    {
        var uiState = UIState.Instance();
        if (uiState == null)
        {
            Service.Log.Error("UIState is null");
            return false;
        }

        var hasStats =
            uiState->PlayerState.Attributes[70] >= craftsmanship &&
            uiState->PlayerState.Attributes[71] >= control &&
            uiState->PlayerState.Attributes[11] >= cp;

        return hasStats;
    }

    /// <inheritdoc/>
    public unsafe bool HasStatus(string statusName)
    {
        statusName = statusName.ToLowerInvariant();
        var sheet = Service.DataManager.GetExcelSheet<Lumina.Excel.GeneratedSheets.Status>()!;
        var statusIDs = sheet
            .Where(row => row.Name.RawString.ToLowerInvariant() == statusName)
            .Select(row => row.RowId)
            .ToArray()!;

        return this.HasStatusId(statusIDs);
    }

    /// <inheritdoc/>
    public unsafe bool HasStatusId(params uint[] statusIDs)
    {
        var statusID = Service.ClientState.LocalPlayer!.StatusList
            .Select(se => se.StatusId)
            .ToList().Intersect(statusIDs)
            .FirstOrDefault();

        return statusID != default;
    }

    /// <inheritdoc/>
    public unsafe bool IsAddonVisible(string addonName)
    {
        var ptr = Service.GameGui.GetAddonByName(addonName, 1);
        if (ptr == IntPtr.Zero)
            return false;

        var addon = (AtkUnitBase*)ptr;
        return addon->IsVisible;
    }

    /// <inheritdoc/>
    public unsafe bool IsAddonReady(string addonName)
    {
        var ptr = Service.GameGui.GetAddonByName(addonName, 1);
        if (ptr == IntPtr.Zero)
            return false;

        var addon = (AtkUnitBase*)ptr;
        return addon->UldManager.LoadedState == AtkLoadState.Loaded;
    }

    /// <inheritdoc/>
    public unsafe string GetNodeText(string addonName, params int[] nodeNumbers)
    {
        if (nodeNumbers.Length == 0)
            throw new MacroCommandError("At least one node number is required");

        var ptr = Service.GameGui.GetAddonByName(addonName, 1);
        if (ptr == IntPtr.Zero)
            throw new MacroCommandError($"Could not find {addonName} addon");

        var addon = (AtkUnitBase*)ptr;
        var uld = addon->UldManager;

        AtkResNode* node = null;
        var debugString = string.Empty;
        for (var i = 0; i < nodeNumbers.Length; i++)
        {
            var nodeNumber = nodeNumbers[i];

            var count = uld.NodeListCount;
            if (nodeNumber < 0 || nodeNumber >= count)
                throw new MacroCommandError($"Addon node number must be between 0 and {count} for the {addonName} addon");

            node = uld.NodeList[nodeNumber];
            debugString += $"[{nodeNumber}]";

            if (node == null)
                throw new MacroCommandError($"{addonName} addon node{debugString} is null");

            // More nodes to traverse
            if (i < nodeNumbers.Length - 1)
            {
                if ((int)node->Type < 1000)
                    throw new MacroCommandError($"{addonName} addon node{debugString} is not a component");

                uld = ((AtkComponentNode*)node)->Component->UldManager;
            }
        }

        if (node->Type != NodeType.Text)
            throw new MacroCommandError($"{addonName} addon node{debugString} is not a text node");

        var textNode = (AtkTextNode*)node;
        return textNode->NodeText.ToString();
    }

    /// <inheritdoc/>
    public unsafe string GetSelectStringText(int index)
    {
        var ptr = Service.GameGui.GetAddonByName("SelectString", 1);
        if (ptr == IntPtr.Zero)
            throw new MacroCommandError("Could not find SelectString addon");

        var addon = (AddonSelectString*)ptr;
        var popup = &addon->PopupMenu.PopupMenu;

        var count = popup->EntryCount;
        Service.Log.Debug($"index={index} // Count={count} // {index < 0 || index > count}");
        if (index < 0 || index > count)
            throw new MacroCommandError("Index out of range");

        var textPtr = popup->EntryNames[index];
        if (textPtr == null)
            throw new MacroCommandError("Text pointer was null");

        return Marshal.PtrToStringUTF8((IntPtr)textPtr) ?? string.Empty;
    }

    /// <inheritdoc/>
    public unsafe string GetSelectIconStringText(int index)
    {
        var ptr = Service.GameGui.GetAddonByName("SelectIconString", 1);
        if (ptr == IntPtr.Zero)
            throw new MacroCommandError("Could not find SelectIconString addon");

        var addon = (AddonSelectIconString*)ptr;
        var popup = &addon->PopupMenu.PopupMenu;

        var count = popup->EntryCount;
        if (index < 0 || index > count)
            throw new MacroCommandError("Index out of range");

        var textPtr = popup->EntryNames[index];
        if (textPtr == null)
            throw new MacroCommandError("Text pointer was null");

        return Marshal.PtrToStringUTF8((IntPtr)textPtr) ?? string.Empty;
    }

    /// <inheritdoc/>
    public bool GetCharacterCondition(int flagID, bool hasCondition = true)
    {
        return hasCondition ? Service.Condition[flagID] : !Service.Condition[flagID];
    }

    /// <inheritdoc/>
    public bool IsInZone(int zoneID) =>
        Service.ClientState.TerritoryType == zoneID;

    /// <inheritdoc/>
    public int GetZoneID() =>
        Service.ClientState.TerritoryType;

    /// <inheritdoc/>
    public string GetCharacterName(bool includeWorld = false) =>
        Service.ClientState.LocalPlayer == null ? "null"
        : includeWorld ? $"{Service.ClientState.LocalPlayer.Name}@{Service.ClientState.LocalPlayer.HomeWorld.GameData!.Name}"
        : Service.ClientState.LocalPlayer.Name.ToString();

    /// <inheritdoc/>
    public unsafe int GetItemCount(int itemID, bool includeHQ = true) =>
        includeHQ ? InventoryManager.Instance()->GetInventoryItemCount((uint)itemID, true) + InventoryManager.Instance()->GetInventoryItemCount((uint)itemID)
        : InventoryManager.Instance()->GetInventoryItemCount((uint)itemID);

    /// <inheritdoc/>
    public unsafe bool DeliverooIsTurnInRunning()
    {
        DeliverooIPC.Init();
        return DeliverooIPC.IsTurnInRunning!.InvokeFunc();
    }

    /// <inheritdoc/>
    public unsafe uint GetProgressIncrease(uint actionID) => this.GetActionResult(actionID).Progress;

    /// <inheritdoc/>
    public unsafe uint GetQualityIncrease(uint actionID) => this.GetActionResult(actionID).Quality;

    private unsafe (uint Progress, uint Quality) GetActionResult(uint id)
    {

        var agent = AgentCraftActionSimulator.Instance();
        if (agent == null) return (0, 0);

        var progress = 0U;
        var quality = 0U;

        // Find Progress
        var p = (ProgressEfficiencyCalculation*)agent->Progress;
        for (var i = 0; i < sizeof(ProgressEfficiencyCalculations) / sizeof(ProgressEfficiencyCalculation); i++)
        {
            if (p == null) break;
            if (p->ActionId == id)
            {
                progress = p->ProgressIncrease;
                break;
            }

            p++;
        }

        var q = (QualityEfficiencyCalculation*)agent->Quality;
        for (var i = 0; i < sizeof(QualityEfficiencyCalculations) / sizeof(QualityEfficiencyCalculation); i++)
        {
            if (q == null) break;
            if (q->ActionId == id)
            {
                quality = q->QualityIncrease;
                break;
            }

            q++;
        }

        return (progress, quality);
    }

    /// <inheritdoc/>
    public void LeaveDuty() => this.abandonDuty(false);

    public bool IsLocalPlayerNull() => Service.ClientState.LocalPlayer == null;

    public bool IsPlayerDead() => Service.ClientState.LocalPlayer!.IsDead;

    public bool IsPlayerCasting() => Service.ClientState.LocalPlayer!.IsCasting;

    public unsafe bool IsMoving() => AgentMap.Instance()->IsPlayerMoving == 1;

    public unsafe uint GetGil() => InventoryManager.Instance()->GetGil();

    public uint GetClassJobId() => Svc.ClientState.LocalPlayer!.ClassJob.Id;

    public float GetPlayerRawXPos() => Svc.ClientState.LocalPlayer!.Position.X;
    public float GetPlayerRawYPos() => Svc.ClientState.LocalPlayer!.Position.Y;
    public float GetPlayerRawZPos() => Svc.ClientState.LocalPlayer!.Position.Z;

    public float GetDistanceToPoint(float x, float y, float z) => Vector3.Distance(Svc.ClientState.LocalPlayer!.Position, new Vector3(x, y, z));

    public unsafe int GetLevel(int expArrayIndex = -1)
    {
        if (expArrayIndex == -1) expArrayIndex = Svc.ClientState.LocalPlayer!.ClassJob.GameData!.ExpArrayIndex;
        return UIState.Instance()->PlayerState.ClassJobLevelArray[expArrayIndex];
    }

    public unsafe int GetFCRank() => ((InfoProxyFreeCompany*)Framework.Instance()->UIModule->GetInfoModule()->GetInfoProxyById(InfoProxyId.FreeCompany))->Rank;

    private unsafe int GetNodeTextAsInt(AtkTextNode* node, string error)
    {
        try
        {
            if (node == null)
                throw new NullReferenceException("TextNode is null");

            var text = node->NodeText.ToString();
            var value = int.Parse(text);
            return value;
        }
        catch (Exception ex)
        {
            throw new MacroCommandError(error, ex);
        }
    }

    private unsafe AddonSynthesis* GetSynthesisAddon()
    {
        var ptr = Service.GameGui.GetAddonByName("Synthesis", 1);
        if (ptr == IntPtr.Zero)
            throw new MacroCommandError("Could not find Synthesis addon");

        return (AddonSynthesis*)ptr;
    }
}
