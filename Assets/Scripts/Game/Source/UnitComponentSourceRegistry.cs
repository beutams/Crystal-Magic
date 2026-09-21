// AUTO-GENERATED - DO NOT EDIT MANUALLY
// Use menu: Tools/Registry/Unit Sources

using System;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public enum UnitSourceId : ushort
{
    None = 0,
    GameInteractionCandidateKind = 1,
    GameInteractionCandidateTarget = 2,
    GameInteractionHasCandidate = 3,
    PlayerInputEscapeHeld = 4,
    PlayerInputInteractHeld = 5,
    PlayerInputInventoryHeld = 6,
    PlayerInputMove = 7,
    PlayerInputPointerWorldPosition = 9,
    PlayerInputPrimaryHeld = 10,
    PlayerInputPropertyHeld = 11,
    PlayerInputPropIndex = 12,
    PlayerInputSkillChainIndex = 13,
    PlayerInputSkillHeld = 14,
    PlayerInputUsePropHeld = 15,
    PlayerSkillCurrentAdditionId = 16,
    PlayerSkillCurrentChainId = 17,
    PlayerSkillCurrentChainSlotClear = 18,
    PlayerSkillCurrentChainSlotSet = 19,
    PlayerSkillCurrentInputType = 20,
    PlayerSkillCurrentSkillId = 21,
    PlayerSkillCurrentSlotIndex = 22,
    PlayerSkillGetChainLength = 23,
    PlayerSkillGetChainSkillAdditionId = 24,
    PlayerSkillGetChainSkillId = 25,
    PlayerSkillGetCurrentChainLength = 27,
    PlayerSkillGetCurrentSkillAdditionId = 28,
    PlayerSkillGetCurrentSkillChantDuration = 29,
    PlayerSkillGetCurrentSkillId = 30,
    PlayerSkillGetCurrentSkillMpCost = 31,
    PlayerSkillGetCurrentSkillRuntimeType = 32,
    PlayerSkillGetInputType = 33,
    PlayerSkillGetSkillCastingMoveMultiplier = 34,
    PlayerSkillGetSkillChantDuration = 35,
    PlayerSkillGetSkillMpCost = 36,
    PlayerSkillGetSkillRuntimeType = 37,
    PlayerSkillHasCurrentSkill = 38,
    PlayerSkillHasCurrentSkillAt = 39,
    PlayerSkillHasSkill = 40,
    PlayerSkillIsChainEmpty = 41,
    PlayerSkillIsCurrentChainEmpty = 42,
    PlayerSkillPendingExtraModifiersAdd = 43,
    UnitAnimationName = 44,
    UnitAnimationPlay = 45,
    UnitAnimationSetName = 46,
    UnitAttackBaseAttackPower = 47,
    UnitAttackBaseChantSpeedBonus = 48,
    UnitAttackBaseSkillRange = 49,
    UnitAttackChantDurationMultiplier = 50,
    UnitAttackRealAttackPower = 51,
    UnitAttackRealChantSpeedBonus = 52,
    UnitAttackRealSkillRange = 53,
    UnitBuffsCount = 54,
    UnitBuffsFindIndex = 55,
    UnitBuffsHas = 56,
    UnitBuffsHasOriginAt = 57,
    UnitBuffsIdAt = 58,
    UnitBuffsOriginEntityAt = 59,
    UnitBuffsRemainingTimeAt = 60,
    UnitBuffsRemove = 61,
    UnitBuffsRemoveStacks = 62,
    UnitBuffsSourceSkillIdAt = 63,
    UnitBuffsStackCount = 64,
    UnitBuffsStackCountAt = 65,
    UnitControlActiveMotionDamping = 66,
    UnitControlActiveMotionVelocity = 67,
    UnitControlActivePriority = 68,
    UnitControlActiveRemainingTime = 69,
    UnitControlActiveSourceEntity = 70,
    UnitControlActiveType = 71,
    UnitControlEntryCount = 72,
    UnitControlEntryInterruptOnApplyAt = 73,
    UnitControlEntryLockCastAt = 74,
    UnitControlEntryLockMoveAt = 75,
    UnitControlEntryMotionDampingAt = 76,
    UnitControlEntryMotionVelocityAt = 77,
    UnitControlEntryPriorityAt = 78,
    UnitControlEntryRemainingTimeAt = 79,
    UnitControlEntrySourceEntityAt = 80,
    UnitControlEntryTypeAt = 81,
    UnitControlHasControl = 82,
    UnitControlLockCast = 83,
    UnitControlLockMove = 84,
    UnitDeathIsActive = 85,
    UnitElementFirePower = 86,
    UnitElementLightningPower = 87,
    UnitElementWaterPower = 88,
    UnitElementWindPower = 89,
    UnitFacingDirection = 90,
    UnitFacingSetDirection = 91,
    UnitFactionIsEnemyTo = 92,
    UnitFactionValue = 93,
    UnitInterestPointArrivalDistance = 94,
    UnitInterestPointCurrentTarget = 95,
    UnitInterestPointEncounterId = 96,
    UnitInterestPointHasPatrol = 97,
    UnitInterestPointPatrolSpeed = 98,
    UnitInterestPointPlayerDistance = 99,
    UnitInterestPointSetNextPatrolTarget = 100,
    UnitInterestPointSetPatrolActive = 101,
    UnitInterestPointSetPatrolSpeed = 102,
    UnitInterestPointShouldSpawnPatrol = 103,
    UnitInterestPointSpawnDistance = 104,
    UnitInterestPointSquadId = 105,
    UnitInterestPointTargetReached = 106,
    UnitManaBaseMaxMp = 107,
    UnitManaBaseMaxMpOffset = 108,
    UnitManaBaseMpRegenPerSecond = 109,
    UnitManaBaseMpRegenPerSecondOffset = 110,
    UnitManaCost = 111,
    UnitManaCurrentMana = 112,
    UnitManaCurrentManaPercentage = 113,
    UnitManaRealMaxMp = 114,
    UnitManaRealMpRegenPerSecond = 115,
    UnitMoveBaseMaxAcceleration = 116,
    UnitMoveBaseMoveSpeed = 117,
    UnitMoveCommandSpeed = 118,
    UnitMoveDirection = 119,
    UnitMoveRealMaxAcceleration = 120,
    UnitMoveRealMoveSpeed = 121,
    UnitMoveSetCommandSpeed = 122,
    UnitMoveSetDirection = 123,
    UnitMoveSetFrameVelocity = 124,
    UnitMoveSetStateMoveMultiplier = 125,
    UnitMoveSetVelocity = 126,
    UnitMoveStateMoveMultiplier = 127,
    UnitNavigationSetDestination = 128,
    UnitNavigationStop = 129,
    UnitPerceptionNearestUnitByFaction = 130,
    UnitPerceptionNearestUnitByName = 131,
    UnitPerceptionSearchRadius = 132,
    UnitPerceptionUnitAt = 133,
    UnitPerceptionUnitByFactionAt = 134,
    UnitPerceptionUnitByNameAt = 135,
    UnitPerceptionUnitCount = 136,
    UnitPerceptionUnitName = 137,
    UnitPerceptionUnitsByFactionCount = 138,
    UnitPerceptionUnitsByNameCount = 139,
    UnitSelfEntity = 140,
    UnitTransformDirectionTo = 141,
    UnitTransformForward = 142,
    UnitTransformPosition = 143,
    UnitTransformPositionOf = 144,
    UnitTransformScale = 145,
    UnitVariablesConsumerCount = 146,
    UnitVariablesCount = 147,
    UnitVariablesGet = 148,
    UnitVariablesGetBool = 149,
    UnitVariablesGetEntity = 150,
    UnitVariablesGetFloat2 = 151,
    UnitVariablesGetFloat3 = 152,
    UnitVariablesGetNumber = 153,
    UnitVariablesGetString = 154,
    UnitVariablesHas = 155,
    UnitVariablesOther = 156,
    UnitVariablesRemove = 157,
    UnitVariablesSet = 158,
    UnitVariablesSetOther = 159,
    UnitVitalityBaseDefense = 160,
    UnitVitalityBaseHealthRegenPerSecond = 161,
    UnitVitalityBaseMaxHealth = 162,
    UnitVitalityCurrentHealth = 163,
    UnitVitalityCurrentHealthPercentage = 164,
    UnitVitalityRealDefense = 165,
    UnitVitalityRealHealthRegenPerSecond = 166,
    UnitVitalityRealMaxHealth = 167,
    WorldInteractionIsInteracting = 168,
    WorldVariablesCount = 169,
    WorldVariablesGet = 170,
    WorldVariablesGetBool = 171,
    WorldVariablesGetEntity = 172,
    WorldVariablesGetFloat2 = 173,
    WorldVariablesGetFloat3 = 174,
    WorldVariablesGetNumber = 175,
    WorldVariablesGetString = 176,
    WorldVariablesHas = 177,
    WorldVariablesRemove = 178,
    WorldVariablesSet = 179,
}

public static class UnitComponentSourceRegistry
{
    public const string InteractionRequestKey = "game.interaction.candidate";

    public static bool TryGetGet(string key, out UnitSourceId sourceId, out UnitSourceGetSchemaEntry schema)
    {
        switch (key)
        {
            case "game.interaction.candidateKind":
                sourceId = UnitSourceId.GameInteractionCandidateKind;
                schema = new UnitSourceGetSchemaEntry("game.interaction.candidateKind", typeof(InteractionCandidateComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "game.interaction.candidateTarget":
                sourceId = UnitSourceId.GameInteractionCandidateTarget;
                schema = new UnitSourceGetSchemaEntry("game.interaction.candidateTarget", typeof(InteractionCandidateComponent), UnitValueCategory.Entity, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "game.interaction.hasCandidate":
                sourceId = UnitSourceId.GameInteractionHasCandidate;
                schema = new UnitSourceGetSchemaEntry("game.interaction.hasCandidate", typeof(InteractionCandidateComponent), UnitValueCategory.Bool, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "player.input.escapeHeld":
                sourceId = UnitSourceId.PlayerInputEscapeHeld;
                schema = new UnitSourceGetSchemaEntry("player.input.escapeHeld", typeof(PlayerInputComponent), UnitValueCategory.Bool, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "player.input.interactHeld":
                sourceId = UnitSourceId.PlayerInputInteractHeld;
                schema = new UnitSourceGetSchemaEntry("player.input.interactHeld", typeof(PlayerInputComponent), UnitValueCategory.Bool, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "player.input.inventoryHeld":
                sourceId = UnitSourceId.PlayerInputInventoryHeld;
                schema = new UnitSourceGetSchemaEntry("player.input.inventoryHeld", typeof(PlayerInputComponent), UnitValueCategory.Bool, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "player.input.move":
                sourceId = UnitSourceId.PlayerInputMove;
                schema = new UnitSourceGetSchemaEntry("player.input.move", typeof(PlayerInputComponent), UnitValueCategory.Float2, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "player.input.pointerWorldPosition":
                sourceId = UnitSourceId.PlayerInputPointerWorldPosition;
                schema = new UnitSourceGetSchemaEntry("player.input.pointerWorldPosition", typeof(PlayerInputComponent), UnitValueCategory.Float3, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "player.input.primaryHeld":
                sourceId = UnitSourceId.PlayerInputPrimaryHeld;
                schema = new UnitSourceGetSchemaEntry("player.input.primaryHeld", typeof(PlayerInputComponent), UnitValueCategory.Bool, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "player.input.propertyHeld":
                sourceId = UnitSourceId.PlayerInputPropertyHeld;
                schema = new UnitSourceGetSchemaEntry("player.input.propertyHeld", typeof(PlayerInputComponent), UnitValueCategory.Bool, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "player.input.propIndex":
                sourceId = UnitSourceId.PlayerInputPropIndex;
                schema = new UnitSourceGetSchemaEntry("player.input.propIndex", typeof(PlayerInputComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "player.input.skillChainIndex":
                sourceId = UnitSourceId.PlayerInputSkillChainIndex;
                schema = new UnitSourceGetSchemaEntry("player.input.skillChainIndex", typeof(PlayerInputComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "player.input.skillHeld":
                sourceId = UnitSourceId.PlayerInputSkillHeld;
                schema = new UnitSourceGetSchemaEntry("player.input.skillHeld", typeof(PlayerInputComponent), UnitValueCategory.Bool, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "player.input.usePropHeld":
                sourceId = UnitSourceId.PlayerInputUsePropHeld;
                schema = new UnitSourceGetSchemaEntry("player.input.usePropHeld", typeof(PlayerInputComponent), UnitValueCategory.Bool, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "player.skill.currentAdditionId":
                sourceId = UnitSourceId.PlayerSkillCurrentAdditionId;
                schema = new UnitSourceGetSchemaEntry("player.skill.currentAdditionId", typeof(PlayerCurrentSkillComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "player.skill.currentChainId":
                sourceId = UnitSourceId.PlayerSkillCurrentChainId;
                schema = new UnitSourceGetSchemaEntry("player.skill.currentChainId", typeof(PlayerCurrentSkillComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "player.skill.currentInputType":
                sourceId = UnitSourceId.PlayerSkillCurrentInputType;
                schema = new UnitSourceGetSchemaEntry("player.skill.currentInputType", typeof(PlayerCurrentSkillComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "player.skill.currentSkillId":
                sourceId = UnitSourceId.PlayerSkillCurrentSkillId;
                schema = new UnitSourceGetSchemaEntry("player.skill.currentSkillId", typeof(PlayerCurrentSkillComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "player.skill.currentSlotIndex":
                sourceId = UnitSourceId.PlayerSkillCurrentSlotIndex;
                schema = new UnitSourceGetSchemaEntry("player.skill.currentSlotIndex", typeof(PlayerCurrentSkillComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "player.skill.getChainLength":
                sourceId = UnitSourceId.PlayerSkillGetChainLength;
                schema = new UnitSourceGetSchemaEntry("player.skill.getChainLength", typeof(PlayerInputComponent), UnitValueCategory.Number, new[] { new ComparatorParameterDefinition("Chain ID", UnitValueCategory.Number) });
                return true;
            case "player.skill.getChainSkillAdditionId":
                sourceId = UnitSourceId.PlayerSkillGetChainSkillAdditionId;
                schema = new UnitSourceGetSchemaEntry("player.skill.getChainSkillAdditionId", typeof(PlayerInputComponent), UnitValueCategory.Number, new[] { new ComparatorParameterDefinition("Chain ID", UnitValueCategory.Number), new ComparatorParameterDefinition("Slot Index", UnitValueCategory.Number) });
                return true;
            case "player.skill.getChainSkillId":
                sourceId = UnitSourceId.PlayerSkillGetChainSkillId;
                schema = new UnitSourceGetSchemaEntry("player.skill.getChainSkillId", typeof(PlayerInputComponent), UnitValueCategory.Number, new[] { new ComparatorParameterDefinition("Chain ID", UnitValueCategory.Number), new ComparatorParameterDefinition("Slot Index", UnitValueCategory.Number) });
                return true;
            case "player.skill.getCurrentChainLength":
                sourceId = UnitSourceId.PlayerSkillGetCurrentChainLength;
                schema = new UnitSourceGetSchemaEntry("player.skill.getCurrentChainLength", typeof(PlayerInputComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "player.skill.getCurrentSkillAdditionId":
                sourceId = UnitSourceId.PlayerSkillGetCurrentSkillAdditionId;
                schema = new UnitSourceGetSchemaEntry("player.skill.getCurrentSkillAdditionId", typeof(PlayerInputComponent), UnitValueCategory.Number, new[] { new ComparatorParameterDefinition("Slot Index", UnitValueCategory.Number) });
                return true;
            case "player.skill.getCurrentSkillChantDuration":
                sourceId = UnitSourceId.PlayerSkillGetCurrentSkillChantDuration;
                schema = new UnitSourceGetSchemaEntry("player.skill.getCurrentSkillChantDuration", typeof(PlayerInputComponent), UnitValueCategory.Number, new[] { new ComparatorParameterDefinition("Slot Index", UnitValueCategory.Number) });
                return true;
            case "player.skill.getCurrentSkillId":
                sourceId = UnitSourceId.PlayerSkillGetCurrentSkillId;
                schema = new UnitSourceGetSchemaEntry("player.skill.getCurrentSkillId", typeof(PlayerInputComponent), UnitValueCategory.Number, new[] { new ComparatorParameterDefinition("Slot Index", UnitValueCategory.Number) });
                return true;
            case "player.skill.getCurrentSkillMpCost":
                sourceId = UnitSourceId.PlayerSkillGetCurrentSkillMpCost;
                schema = new UnitSourceGetSchemaEntry("player.skill.getCurrentSkillMpCost", typeof(PlayerInputComponent), UnitValueCategory.Number, new[] { new ComparatorParameterDefinition("Slot Index", UnitValueCategory.Number) });
                return true;
            case "player.skill.getCurrentSkillRuntimeType":
                sourceId = UnitSourceId.PlayerSkillGetCurrentSkillRuntimeType;
                schema = new UnitSourceGetSchemaEntry("player.skill.getCurrentSkillRuntimeType", typeof(PlayerInputComponent), UnitValueCategory.String, new[] { new ComparatorParameterDefinition("Slot Index", UnitValueCategory.Number) });
                return true;
            case "player.skill.getInputType":
                sourceId = UnitSourceId.PlayerSkillGetInputType;
                schema = new UnitSourceGetSchemaEntry("player.skill.getInputType", typeof(PlayerInputComponent), UnitValueCategory.Number, new[] { new ComparatorParameterDefinition("Skill ID", UnitValueCategory.Number) });
                return true;
            case "player.skill.getSkillCastingMoveMultiplier":
                sourceId = UnitSourceId.PlayerSkillGetSkillCastingMoveMultiplier;
                schema = new UnitSourceGetSchemaEntry("player.skill.getSkillCastingMoveMultiplier", typeof(PlayerInputComponent), UnitValueCategory.Number, new[] { new ComparatorParameterDefinition("Skill ID", UnitValueCategory.Number) });
                return true;
            case "player.skill.getSkillChantDuration":
                sourceId = UnitSourceId.PlayerSkillGetSkillChantDuration;
                schema = new UnitSourceGetSchemaEntry("player.skill.getSkillChantDuration", typeof(PlayerInputComponent), UnitValueCategory.Number, new[] { new ComparatorParameterDefinition("Skill ID", UnitValueCategory.Number) });
                return true;
            case "player.skill.getSkillMpCost":
                sourceId = UnitSourceId.PlayerSkillGetSkillMpCost;
                schema = new UnitSourceGetSchemaEntry("player.skill.getSkillMpCost", typeof(PlayerInputComponent), UnitValueCategory.Number, new[] { new ComparatorParameterDefinition("Skill ID", UnitValueCategory.Number) });
                return true;
            case "player.skill.getSkillRuntimeType":
                sourceId = UnitSourceId.PlayerSkillGetSkillRuntimeType;
                schema = new UnitSourceGetSchemaEntry("player.skill.getSkillRuntimeType", typeof(PlayerInputComponent), UnitValueCategory.String, new[] { new ComparatorParameterDefinition("Skill ID", UnitValueCategory.Number) });
                return true;
            case "player.skill.hasCurrentSkill":
                sourceId = UnitSourceId.PlayerSkillHasCurrentSkill;
                schema = new UnitSourceGetSchemaEntry("player.skill.hasCurrentSkill", typeof(PlayerCurrentSkillComponent), UnitValueCategory.Bool, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "player.skill.hasCurrentSkillAt":
                sourceId = UnitSourceId.PlayerSkillHasCurrentSkillAt;
                schema = new UnitSourceGetSchemaEntry("player.skill.hasCurrentSkillAt", typeof(PlayerInputComponent), UnitValueCategory.Bool, new[] { new ComparatorParameterDefinition("Slot Index", UnitValueCategory.Number) });
                return true;
            case "player.skill.hasSkill":
                sourceId = UnitSourceId.PlayerSkillHasSkill;
                schema = new UnitSourceGetSchemaEntry("player.skill.hasSkill", typeof(PlayerInputComponent), UnitValueCategory.Bool, new[] { new ComparatorParameterDefinition("Skill ID", UnitValueCategory.Number) });
                return true;
            case "player.skill.isChainEmpty":
                sourceId = UnitSourceId.PlayerSkillIsChainEmpty;
                schema = new UnitSourceGetSchemaEntry("player.skill.isChainEmpty", typeof(PlayerInputComponent), UnitValueCategory.Bool, new[] { new ComparatorParameterDefinition("Chain ID", UnitValueCategory.Number) });
                return true;
            case "player.skill.isCurrentChainEmpty":
                sourceId = UnitSourceId.PlayerSkillIsCurrentChainEmpty;
                schema = new UnitSourceGetSchemaEntry("player.skill.isCurrentChainEmpty", typeof(PlayerInputComponent), UnitValueCategory.Bool, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.animation.name":
                sourceId = UnitSourceId.UnitAnimationName;
                schema = new UnitSourceGetSchemaEntry("unit.animation.name", typeof(UnitAnimationComponent), UnitValueCategory.String, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.attack.baseAttackPower":
                sourceId = UnitSourceId.UnitAttackBaseAttackPower;
                schema = new UnitSourceGetSchemaEntry("unit.attack.baseAttackPower", typeof(UnitAttackComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.attack.baseChantSpeedBonus":
                sourceId = UnitSourceId.UnitAttackBaseChantSpeedBonus;
                schema = new UnitSourceGetSchemaEntry("unit.attack.baseChantSpeedBonus", typeof(UnitAttackComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.attack.baseSkillRange":
                sourceId = UnitSourceId.UnitAttackBaseSkillRange;
                schema = new UnitSourceGetSchemaEntry("unit.attack.baseSkillRange", typeof(UnitAttackComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.attack.chantDurationMultiplier":
                sourceId = UnitSourceId.UnitAttackChantDurationMultiplier;
                schema = new UnitSourceGetSchemaEntry("unit.attack.chantDurationMultiplier", typeof(UnitAttackComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.attack.realAttackPower":
                sourceId = UnitSourceId.UnitAttackRealAttackPower;
                schema = new UnitSourceGetSchemaEntry("unit.attack.realAttackPower", typeof(UnitAttackComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.attack.realChantSpeedBonus":
                sourceId = UnitSourceId.UnitAttackRealChantSpeedBonus;
                schema = new UnitSourceGetSchemaEntry("unit.attack.realChantSpeedBonus", typeof(UnitAttackComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.attack.realSkillRange":
                sourceId = UnitSourceId.UnitAttackRealSkillRange;
                schema = new UnitSourceGetSchemaEntry("unit.attack.realSkillRange", typeof(UnitAttackComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.buffs.count":
                sourceId = UnitSourceId.UnitBuffsCount;
                schema = new UnitSourceGetSchemaEntry("unit.buffs.count", typeof(UnitBuffComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.buffs.findIndex":
                sourceId = UnitSourceId.UnitBuffsFindIndex;
                schema = new UnitSourceGetSchemaEntry("unit.buffs.findIndex", typeof(UnitBuffComponent), UnitValueCategory.Number, new[] { new ComparatorParameterDefinition("BuffId", UnitValueCategory.Number) });
                return true;
            case "unit.buffs.has":
                sourceId = UnitSourceId.UnitBuffsHas;
                schema = new UnitSourceGetSchemaEntry("unit.buffs.has", typeof(UnitBuffComponent), UnitValueCategory.Bool, new[] { new ComparatorParameterDefinition("BuffId", UnitValueCategory.Number) });
                return true;
            case "unit.buffs.hasOriginAt":
                sourceId = UnitSourceId.UnitBuffsHasOriginAt;
                schema = new UnitSourceGetSchemaEntry("unit.buffs.hasOriginAt", typeof(UnitBuffComponent), UnitValueCategory.Bool, new[] { new ComparatorParameterDefinition("Index", UnitValueCategory.Number) });
                return true;
            case "unit.buffs.idAt":
                sourceId = UnitSourceId.UnitBuffsIdAt;
                schema = new UnitSourceGetSchemaEntry("unit.buffs.idAt", typeof(UnitBuffComponent), UnitValueCategory.Number, new[] { new ComparatorParameterDefinition("Index", UnitValueCategory.Number) });
                return true;
            case "unit.buffs.originEntityAt":
                sourceId = UnitSourceId.UnitBuffsOriginEntityAt;
                schema = new UnitSourceGetSchemaEntry("unit.buffs.originEntityAt", typeof(UnitBuffComponent), UnitValueCategory.Entity, new[] { new ComparatorParameterDefinition("Index", UnitValueCategory.Number) });
                return true;
            case "unit.buffs.remainingTimeAt":
                sourceId = UnitSourceId.UnitBuffsRemainingTimeAt;
                schema = new UnitSourceGetSchemaEntry("unit.buffs.remainingTimeAt", typeof(UnitBuffComponent), UnitValueCategory.Number, new[] { new ComparatorParameterDefinition("Index", UnitValueCategory.Number) });
                return true;
            case "unit.buffs.sourceSkillIdAt":
                sourceId = UnitSourceId.UnitBuffsSourceSkillIdAt;
                schema = new UnitSourceGetSchemaEntry("unit.buffs.sourceSkillIdAt", typeof(UnitBuffComponent), UnitValueCategory.Number, new[] { new ComparatorParameterDefinition("Index", UnitValueCategory.Number) });
                return true;
            case "unit.buffs.stackCount":
                sourceId = UnitSourceId.UnitBuffsStackCount;
                schema = new UnitSourceGetSchemaEntry("unit.buffs.stackCount", typeof(UnitBuffComponent), UnitValueCategory.Number, new[] { new ComparatorParameterDefinition("BuffId", UnitValueCategory.Number) });
                return true;
            case "unit.buffs.stackCountAt":
                sourceId = UnitSourceId.UnitBuffsStackCountAt;
                schema = new UnitSourceGetSchemaEntry("unit.buffs.stackCountAt", typeof(UnitBuffComponent), UnitValueCategory.Number, new[] { new ComparatorParameterDefinition("Index", UnitValueCategory.Number) });
                return true;
            case "unit.control.activeMotionDamping":
                sourceId = UnitSourceId.UnitControlActiveMotionDamping;
                schema = new UnitSourceGetSchemaEntry("unit.control.activeMotionDamping", typeof(UnitControlRuntimeComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.control.activeMotionVelocity":
                sourceId = UnitSourceId.UnitControlActiveMotionVelocity;
                schema = new UnitSourceGetSchemaEntry("unit.control.activeMotionVelocity", typeof(UnitControlRuntimeComponent), UnitValueCategory.Float2, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.control.activePriority":
                sourceId = UnitSourceId.UnitControlActivePriority;
                schema = new UnitSourceGetSchemaEntry("unit.control.activePriority", typeof(UnitControlRuntimeComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.control.activeRemainingTime":
                sourceId = UnitSourceId.UnitControlActiveRemainingTime;
                schema = new UnitSourceGetSchemaEntry("unit.control.activeRemainingTime", typeof(UnitControlRuntimeComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.control.activeSourceEntity":
                sourceId = UnitSourceId.UnitControlActiveSourceEntity;
                schema = new UnitSourceGetSchemaEntry("unit.control.activeSourceEntity", typeof(UnitControlRuntimeComponent), UnitValueCategory.Entity, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.control.activeType":
                sourceId = UnitSourceId.UnitControlActiveType;
                schema = new UnitSourceGetSchemaEntry("unit.control.activeType", typeof(UnitControlRuntimeComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.control.entryCount":
                sourceId = UnitSourceId.UnitControlEntryCount;
                schema = new UnitSourceGetSchemaEntry("unit.control.entryCount", typeof(UnitControlRuntimeComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.control.entryInterruptOnApplyAt":
                sourceId = UnitSourceId.UnitControlEntryInterruptOnApplyAt;
                schema = new UnitSourceGetSchemaEntry("unit.control.entryInterruptOnApplyAt", typeof(UnitControlRuntimeComponent), UnitValueCategory.Bool, new[] { new ComparatorParameterDefinition("Index", UnitValueCategory.Number) });
                return true;
            case "unit.control.entryLockCastAt":
                sourceId = UnitSourceId.UnitControlEntryLockCastAt;
                schema = new UnitSourceGetSchemaEntry("unit.control.entryLockCastAt", typeof(UnitControlRuntimeComponent), UnitValueCategory.Bool, new[] { new ComparatorParameterDefinition("Index", UnitValueCategory.Number) });
                return true;
            case "unit.control.entryLockMoveAt":
                sourceId = UnitSourceId.UnitControlEntryLockMoveAt;
                schema = new UnitSourceGetSchemaEntry("unit.control.entryLockMoveAt", typeof(UnitControlRuntimeComponent), UnitValueCategory.Bool, new[] { new ComparatorParameterDefinition("Index", UnitValueCategory.Number) });
                return true;
            case "unit.control.entryMotionDampingAt":
                sourceId = UnitSourceId.UnitControlEntryMotionDampingAt;
                schema = new UnitSourceGetSchemaEntry("unit.control.entryMotionDampingAt", typeof(UnitControlRuntimeComponent), UnitValueCategory.Number, new[] { new ComparatorParameterDefinition("Index", UnitValueCategory.Number) });
                return true;
            case "unit.control.entryMotionVelocityAt":
                sourceId = UnitSourceId.UnitControlEntryMotionVelocityAt;
                schema = new UnitSourceGetSchemaEntry("unit.control.entryMotionVelocityAt", typeof(UnitControlRuntimeComponent), UnitValueCategory.Float2, new[] { new ComparatorParameterDefinition("Index", UnitValueCategory.Number) });
                return true;
            case "unit.control.entryPriorityAt":
                sourceId = UnitSourceId.UnitControlEntryPriorityAt;
                schema = new UnitSourceGetSchemaEntry("unit.control.entryPriorityAt", typeof(UnitControlRuntimeComponent), UnitValueCategory.Number, new[] { new ComparatorParameterDefinition("Index", UnitValueCategory.Number) });
                return true;
            case "unit.control.entryRemainingTimeAt":
                sourceId = UnitSourceId.UnitControlEntryRemainingTimeAt;
                schema = new UnitSourceGetSchemaEntry("unit.control.entryRemainingTimeAt", typeof(UnitControlRuntimeComponent), UnitValueCategory.Number, new[] { new ComparatorParameterDefinition("Index", UnitValueCategory.Number) });
                return true;
            case "unit.control.entrySourceEntityAt":
                sourceId = UnitSourceId.UnitControlEntrySourceEntityAt;
                schema = new UnitSourceGetSchemaEntry("unit.control.entrySourceEntityAt", typeof(UnitControlRuntimeComponent), UnitValueCategory.Entity, new[] { new ComparatorParameterDefinition("Index", UnitValueCategory.Number) });
                return true;
            case "unit.control.entryTypeAt":
                sourceId = UnitSourceId.UnitControlEntryTypeAt;
                schema = new UnitSourceGetSchemaEntry("unit.control.entryTypeAt", typeof(UnitControlRuntimeComponent), UnitValueCategory.Number, new[] { new ComparatorParameterDefinition("Index", UnitValueCategory.Number) });
                return true;
            case "unit.control.hasControl":
                sourceId = UnitSourceId.UnitControlHasControl;
                schema = new UnitSourceGetSchemaEntry("unit.control.hasControl", typeof(UnitControlRuntimeComponent), UnitValueCategory.Bool, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.control.lockCast":
                sourceId = UnitSourceId.UnitControlLockCast;
                schema = new UnitSourceGetSchemaEntry("unit.control.lockCast", typeof(UnitControlRuntimeComponent), UnitValueCategory.Bool, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.control.lockMove":
                sourceId = UnitSourceId.UnitControlLockMove;
                schema = new UnitSourceGetSchemaEntry("unit.control.lockMove", typeof(UnitControlRuntimeComponent), UnitValueCategory.Bool, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.death.isActive":
                sourceId = UnitSourceId.UnitDeathIsActive;
                schema = new UnitSourceGetSchemaEntry("unit.death.isActive", typeof(UnitDeathComponent), UnitValueCategory.Bool, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.element.firePower":
                sourceId = UnitSourceId.UnitElementFirePower;
                schema = new UnitSourceGetSchemaEntry("unit.element.firePower", typeof(UnitElementComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.element.lightningPower":
                sourceId = UnitSourceId.UnitElementLightningPower;
                schema = new UnitSourceGetSchemaEntry("unit.element.lightningPower", typeof(UnitElementComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.element.waterPower":
                sourceId = UnitSourceId.UnitElementWaterPower;
                schema = new UnitSourceGetSchemaEntry("unit.element.waterPower", typeof(UnitElementComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.element.windPower":
                sourceId = UnitSourceId.UnitElementWindPower;
                schema = new UnitSourceGetSchemaEntry("unit.element.windPower", typeof(UnitElementComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.facing.direction":
                sourceId = UnitSourceId.UnitFacingDirection;
                schema = new UnitSourceGetSchemaEntry("unit.facing.direction", typeof(UnitFacingComponent), UnitValueCategory.Float2, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.faction.isEnemyTo":
                sourceId = UnitSourceId.UnitFactionIsEnemyTo;
                schema = new UnitSourceGetSchemaEntry("unit.faction.isEnemyTo", typeof(UnitFactionComponent), UnitValueCategory.Bool, new[] { new ComparatorParameterDefinition("Other Entity", UnitValueCategory.Entity) });
                return true;
            case "unit.faction.value":
                sourceId = UnitSourceId.UnitFactionValue;
                schema = new UnitSourceGetSchemaEntry("unit.faction.value", typeof(UnitFactionComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.interestPoint.arrivalDistance":
                sourceId = UnitSourceId.UnitInterestPointArrivalDistance;
                schema = new UnitSourceGetSchemaEntry("unit.interestPoint.arrivalDistance", typeof(DungeonInterestPointComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.interestPoint.currentTarget":
                sourceId = UnitSourceId.UnitInterestPointCurrentTarget;
                schema = new UnitSourceGetSchemaEntry("unit.interestPoint.currentTarget", typeof(DungeonInterestPointComponent), UnitValueCategory.Entity, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.interestPoint.encounterId":
                sourceId = UnitSourceId.UnitInterestPointEncounterId;
                schema = new UnitSourceGetSchemaEntry("unit.interestPoint.encounterId", typeof(DungeonInterestPointComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.interestPoint.hasPatrol":
                sourceId = UnitSourceId.UnitInterestPointHasPatrol;
                schema = new UnitSourceGetSchemaEntry("unit.interestPoint.hasPatrol", typeof(DungeonInterestPointComponent), UnitValueCategory.Bool, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.interestPoint.patrolSpeed":
                sourceId = UnitSourceId.UnitInterestPointPatrolSpeed;
                schema = new UnitSourceGetSchemaEntry("unit.interestPoint.patrolSpeed", typeof(DungeonInterestPointComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.interestPoint.playerDistance":
                sourceId = UnitSourceId.UnitInterestPointPlayerDistance;
                schema = new UnitSourceGetSchemaEntry("unit.interestPoint.playerDistance", typeof(DungeonInterestPointComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.interestPoint.shouldSpawnPatrol":
                sourceId = UnitSourceId.UnitInterestPointShouldSpawnPatrol;
                schema = new UnitSourceGetSchemaEntry("unit.interestPoint.shouldSpawnPatrol", typeof(DungeonInterestPointComponent), UnitValueCategory.Bool, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.interestPoint.spawnDistance":
                sourceId = UnitSourceId.UnitInterestPointSpawnDistance;
                schema = new UnitSourceGetSchemaEntry("unit.interestPoint.spawnDistance", typeof(DungeonInterestPointComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.interestPoint.squadId":
                sourceId = UnitSourceId.UnitInterestPointSquadId;
                schema = new UnitSourceGetSchemaEntry("unit.interestPoint.squadId", typeof(DungeonInterestPointComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.interestPoint.targetReached":
                sourceId = UnitSourceId.UnitInterestPointTargetReached;
                schema = new UnitSourceGetSchemaEntry("unit.interestPoint.targetReached", typeof(DungeonInterestPointComponent), UnitValueCategory.Bool, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.mana.baseMaxMp":
                sourceId = UnitSourceId.UnitManaBaseMaxMp;
                schema = new UnitSourceGetSchemaEntry("unit.mana.baseMaxMp", typeof(UnitManaComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.mana.baseMaxMpOffset":
                sourceId = UnitSourceId.UnitManaBaseMaxMpOffset;
                schema = new UnitSourceGetSchemaEntry("unit.mana.baseMaxMpOffset", typeof(UnitManaComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.mana.baseMpRegenPerSecond":
                sourceId = UnitSourceId.UnitManaBaseMpRegenPerSecond;
                schema = new UnitSourceGetSchemaEntry("unit.mana.baseMpRegenPerSecond", typeof(UnitManaComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.mana.baseMpRegenPerSecondOffset":
                sourceId = UnitSourceId.UnitManaBaseMpRegenPerSecondOffset;
                schema = new UnitSourceGetSchemaEntry("unit.mana.baseMpRegenPerSecondOffset", typeof(UnitManaComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.mana.currentMana":
                sourceId = UnitSourceId.UnitManaCurrentMana;
                schema = new UnitSourceGetSchemaEntry("unit.mana.currentMana", typeof(UnitManaComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.mana.currentManaPercentage":
                sourceId = UnitSourceId.UnitManaCurrentManaPercentage;
                schema = new UnitSourceGetSchemaEntry("unit.mana.currentManaPercentage", typeof(UnitManaComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.mana.realMaxMp":
                sourceId = UnitSourceId.UnitManaRealMaxMp;
                schema = new UnitSourceGetSchemaEntry("unit.mana.realMaxMp", typeof(UnitManaComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.mana.realMpRegenPerSecond":
                sourceId = UnitSourceId.UnitManaRealMpRegenPerSecond;
                schema = new UnitSourceGetSchemaEntry("unit.mana.realMpRegenPerSecond", typeof(UnitManaComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.move.baseMaxAcceleration":
                sourceId = UnitSourceId.UnitMoveBaseMaxAcceleration;
                schema = new UnitSourceGetSchemaEntry("unit.move.baseMaxAcceleration", typeof(UnitMoveComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.move.baseMoveSpeed":
                sourceId = UnitSourceId.UnitMoveBaseMoveSpeed;
                schema = new UnitSourceGetSchemaEntry("unit.move.baseMoveSpeed", typeof(UnitMoveComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.move.commandSpeed":
                sourceId = UnitSourceId.UnitMoveCommandSpeed;
                schema = new UnitSourceGetSchemaEntry("unit.move.commandSpeed", typeof(UnitMoveComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.move.direction":
                sourceId = UnitSourceId.UnitMoveDirection;
                schema = new UnitSourceGetSchemaEntry("unit.move.direction", typeof(UnitMoveComponent), UnitValueCategory.Float2, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.move.realMaxAcceleration":
                sourceId = UnitSourceId.UnitMoveRealMaxAcceleration;
                schema = new UnitSourceGetSchemaEntry("unit.move.realMaxAcceleration", typeof(UnitMoveComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.move.realMoveSpeed":
                sourceId = UnitSourceId.UnitMoveRealMoveSpeed;
                schema = new UnitSourceGetSchemaEntry("unit.move.realMoveSpeed", typeof(UnitMoveComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.move.stateMoveMultiplier":
                sourceId = UnitSourceId.UnitMoveStateMoveMultiplier;
                schema = new UnitSourceGetSchemaEntry("unit.move.stateMoveMultiplier", typeof(UnitMoveComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.perception.nearestUnitByFaction":
                sourceId = UnitSourceId.UnitPerceptionNearestUnitByFaction;
                schema = new UnitSourceGetSchemaEntry("unit.perception.nearestUnitByFaction", typeof(UnitPerceptionComponent), UnitValueCategory.Entity, new[] { new ComparatorParameterDefinition("Faction", UnitValueCategory.Number) });
                return true;
            case "unit.perception.nearestUnitByName":
                sourceId = UnitSourceId.UnitPerceptionNearestUnitByName;
                schema = new UnitSourceGetSchemaEntry("unit.perception.nearestUnitByName", typeof(UnitPerceptionComponent), UnitValueCategory.Entity, new[] { new ComparatorParameterDefinition("Unit Name", UnitValueCategory.String) });
                return true;
            case "unit.perception.searchRadius":
                sourceId = UnitSourceId.UnitPerceptionSearchRadius;
                schema = new UnitSourceGetSchemaEntry("unit.perception.searchRadius", typeof(UnitPerceptionComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.perception.unitAt":
                sourceId = UnitSourceId.UnitPerceptionUnitAt;
                schema = new UnitSourceGetSchemaEntry("unit.perception.unitAt", typeof(UnitPerceptionComponent), UnitValueCategory.Entity, new[] { new ComparatorParameterDefinition("Index", UnitValueCategory.Number) });
                return true;
            case "unit.perception.unitByFactionAt":
                sourceId = UnitSourceId.UnitPerceptionUnitByFactionAt;
                schema = new UnitSourceGetSchemaEntry("unit.perception.unitByFactionAt", typeof(UnitPerceptionComponent), UnitValueCategory.Entity, new[] { new ComparatorParameterDefinition("Faction", UnitValueCategory.Number), new ComparatorParameterDefinition("Index", UnitValueCategory.Number) });
                return true;
            case "unit.perception.unitByNameAt":
                sourceId = UnitSourceId.UnitPerceptionUnitByNameAt;
                schema = new UnitSourceGetSchemaEntry("unit.perception.unitByNameAt", typeof(UnitPerceptionComponent), UnitValueCategory.Entity, new[] { new ComparatorParameterDefinition("Unit Name", UnitValueCategory.String), new ComparatorParameterDefinition("Index", UnitValueCategory.Number) });
                return true;
            case "unit.perception.unitCount":
                sourceId = UnitSourceId.UnitPerceptionUnitCount;
                schema = new UnitSourceGetSchemaEntry("unit.perception.unitCount", typeof(UnitPerceptionComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.perception.unitName":
                sourceId = UnitSourceId.UnitPerceptionUnitName;
                schema = new UnitSourceGetSchemaEntry("unit.perception.unitName", typeof(UnitPerceptionComponent), UnitValueCategory.String, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.perception.unitsByFactionCount":
                sourceId = UnitSourceId.UnitPerceptionUnitsByFactionCount;
                schema = new UnitSourceGetSchemaEntry("unit.perception.unitsByFactionCount", typeof(UnitPerceptionComponent), UnitValueCategory.Number, new[] { new ComparatorParameterDefinition("Faction", UnitValueCategory.Number) });
                return true;
            case "unit.perception.unitsByNameCount":
                sourceId = UnitSourceId.UnitPerceptionUnitsByNameCount;
                schema = new UnitSourceGetSchemaEntry("unit.perception.unitsByNameCount", typeof(UnitPerceptionComponent), UnitValueCategory.Number, new[] { new ComparatorParameterDefinition("Unit Name", UnitValueCategory.String) });
                return true;
            case "unit.self.entity":
                sourceId = UnitSourceId.UnitSelfEntity;
                schema = new UnitSourceGetSchemaEntry("unit.self.entity", typeof(UnitSkillReleaseComponent), UnitValueCategory.Entity, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.transform.directionTo":
                sourceId = UnitSourceId.UnitTransformDirectionTo;
                schema = new UnitSourceGetSchemaEntry("unit.transform.directionTo", typeof(LocalTransform), UnitValueCategory.Float2, new[] { new ComparatorParameterDefinition("Target Unit", UnitValueCategory.Entity) });
                return true;
            case "unit.transform.forward":
                sourceId = UnitSourceId.UnitTransformForward;
                schema = new UnitSourceGetSchemaEntry("unit.transform.forward", typeof(LocalTransform), UnitValueCategory.Float3, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.transform.position":
                sourceId = UnitSourceId.UnitTransformPosition;
                schema = new UnitSourceGetSchemaEntry("unit.transform.position", typeof(LocalTransform), UnitValueCategory.Float3, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.transform.positionOf":
                sourceId = UnitSourceId.UnitTransformPositionOf;
                schema = new UnitSourceGetSchemaEntry("unit.transform.positionOf", typeof(LocalTransform), UnitValueCategory.Float3, new[] { new ComparatorParameterDefinition("Target Unit", UnitValueCategory.Entity) });
                return true;
            case "unit.transform.scale":
                sourceId = UnitSourceId.UnitTransformScale;
                schema = new UnitSourceGetSchemaEntry("unit.transform.scale", typeof(LocalTransform), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.variables.consumerCount":
                sourceId = UnitSourceId.UnitVariablesConsumerCount;
                schema = new UnitSourceGetSchemaEntry("unit.variables.consumerCount", typeof(UnitVariableComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.variables.count":
                sourceId = UnitSourceId.UnitVariablesCount;
                schema = new UnitSourceGetSchemaEntry("unit.variables.count", typeof(UnitVariableComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.variables.get":
                sourceId = UnitSourceId.UnitVariablesGet;
                schema = new UnitSourceGetSchemaEntry("unit.variables.get", typeof(UnitVariableComponent), UnitValueCategory.Any, new[] { new ComparatorParameterDefinition("Key", UnitValueCategory.String) });
                return true;
            case "unit.variables.getBool":
                sourceId = UnitSourceId.UnitVariablesGetBool;
                schema = new UnitSourceGetSchemaEntry("unit.variables.getBool", typeof(UnitVariableComponent), UnitValueCategory.Bool, new[] { new ComparatorParameterDefinition("Key", UnitValueCategory.String) });
                return true;
            case "unit.variables.getEntity":
                sourceId = UnitSourceId.UnitVariablesGetEntity;
                schema = new UnitSourceGetSchemaEntry("unit.variables.getEntity", typeof(UnitVariableComponent), UnitValueCategory.Entity, new[] { new ComparatorParameterDefinition("Key", UnitValueCategory.String) });
                return true;
            case "unit.variables.getFloat2":
                sourceId = UnitSourceId.UnitVariablesGetFloat2;
                schema = new UnitSourceGetSchemaEntry("unit.variables.getFloat2", typeof(UnitVariableComponent), UnitValueCategory.Float2, new[] { new ComparatorParameterDefinition("Key", UnitValueCategory.String) });
                return true;
            case "unit.variables.getFloat3":
                sourceId = UnitSourceId.UnitVariablesGetFloat3;
                schema = new UnitSourceGetSchemaEntry("unit.variables.getFloat3", typeof(UnitVariableComponent), UnitValueCategory.Float3, new[] { new ComparatorParameterDefinition("Key", UnitValueCategory.String) });
                return true;
            case "unit.variables.getNumber":
                sourceId = UnitSourceId.UnitVariablesGetNumber;
                schema = new UnitSourceGetSchemaEntry("unit.variables.getNumber", typeof(UnitVariableComponent), UnitValueCategory.Number, new[] { new ComparatorParameterDefinition("Key", UnitValueCategory.String) });
                return true;
            case "unit.variables.getString":
                sourceId = UnitSourceId.UnitVariablesGetString;
                schema = new UnitSourceGetSchemaEntry("unit.variables.getString", typeof(UnitVariableComponent), UnitValueCategory.String, new[] { new ComparatorParameterDefinition("Key", UnitValueCategory.String) });
                return true;
            case "unit.variables.has":
                sourceId = UnitSourceId.UnitVariablesHas;
                schema = new UnitSourceGetSchemaEntry("unit.variables.has", typeof(UnitVariableComponent), UnitValueCategory.Bool, new[] { new ComparatorParameterDefinition("Key", UnitValueCategory.String) });
                return true;
            case "unit.variables.other":
                sourceId = UnitSourceId.UnitVariablesOther;
                schema = new UnitSourceGetSchemaEntry("unit.variables.other", typeof(UnitVariableComponent), UnitValueCategory.Entity, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.vitality.baseDefense":
                sourceId = UnitSourceId.UnitVitalityBaseDefense;
                schema = new UnitSourceGetSchemaEntry("unit.vitality.baseDefense", typeof(UnitVitalityComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.vitality.baseHealthRegenPerSecond":
                sourceId = UnitSourceId.UnitVitalityBaseHealthRegenPerSecond;
                schema = new UnitSourceGetSchemaEntry("unit.vitality.baseHealthRegenPerSecond", typeof(UnitVitalityComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.vitality.baseMaxHealth":
                sourceId = UnitSourceId.UnitVitalityBaseMaxHealth;
                schema = new UnitSourceGetSchemaEntry("unit.vitality.baseMaxHealth", typeof(UnitVitalityComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.vitality.currentHealth":
                sourceId = UnitSourceId.UnitVitalityCurrentHealth;
                schema = new UnitSourceGetSchemaEntry("unit.vitality.currentHealth", typeof(UnitVitalityComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.vitality.currentHealthPercentage":
                sourceId = UnitSourceId.UnitVitalityCurrentHealthPercentage;
                schema = new UnitSourceGetSchemaEntry("unit.vitality.currentHealthPercentage", typeof(UnitVitalityComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.vitality.realDefense":
                sourceId = UnitSourceId.UnitVitalityRealDefense;
                schema = new UnitSourceGetSchemaEntry("unit.vitality.realDefense", typeof(UnitVitalityComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.vitality.realHealthRegenPerSecond":
                sourceId = UnitSourceId.UnitVitalityRealHealthRegenPerSecond;
                schema = new UnitSourceGetSchemaEntry("unit.vitality.realHealthRegenPerSecond", typeof(UnitVitalityComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "unit.vitality.realMaxHealth":
                sourceId = UnitSourceId.UnitVitalityRealMaxHealth;
                schema = new UnitSourceGetSchemaEntry("unit.vitality.realMaxHealth", typeof(UnitVitalityComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "world.interaction.isInteracting":
                sourceId = UnitSourceId.WorldInteractionIsInteracting;
                schema = new UnitSourceGetSchemaEntry("world.interaction.isInteracting", typeof(InteractionCandidateComponent), UnitValueCategory.Bool, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "world.variables.count":
                sourceId = UnitSourceId.WorldVariablesCount;
                schema = new UnitSourceGetSchemaEntry("world.variables.count", typeof(WorldVariableComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
                return true;
            case "world.variables.get":
                sourceId = UnitSourceId.WorldVariablesGet;
                schema = new UnitSourceGetSchemaEntry("world.variables.get", typeof(WorldVariableComponent), UnitValueCategory.Any, new[] { new ComparatorParameterDefinition("Key", UnitValueCategory.String) });
                return true;
            case "world.variables.getBool":
                sourceId = UnitSourceId.WorldVariablesGetBool;
                schema = new UnitSourceGetSchemaEntry("world.variables.getBool", typeof(WorldVariableComponent), UnitValueCategory.Bool, new[] { new ComparatorParameterDefinition("Key", UnitValueCategory.String) });
                return true;
            case "world.variables.getEntity":
                sourceId = UnitSourceId.WorldVariablesGetEntity;
                schema = new UnitSourceGetSchemaEntry("world.variables.getEntity", typeof(WorldVariableComponent), UnitValueCategory.Entity, new[] { new ComparatorParameterDefinition("Key", UnitValueCategory.String) });
                return true;
            case "world.variables.getFloat2":
                sourceId = UnitSourceId.WorldVariablesGetFloat2;
                schema = new UnitSourceGetSchemaEntry("world.variables.getFloat2", typeof(WorldVariableComponent), UnitValueCategory.Float2, new[] { new ComparatorParameterDefinition("Key", UnitValueCategory.String) });
                return true;
            case "world.variables.getFloat3":
                sourceId = UnitSourceId.WorldVariablesGetFloat3;
                schema = new UnitSourceGetSchemaEntry("world.variables.getFloat3", typeof(WorldVariableComponent), UnitValueCategory.Float3, new[] { new ComparatorParameterDefinition("Key", UnitValueCategory.String) });
                return true;
            case "world.variables.getNumber":
                sourceId = UnitSourceId.WorldVariablesGetNumber;
                schema = new UnitSourceGetSchemaEntry("world.variables.getNumber", typeof(WorldVariableComponent), UnitValueCategory.Number, new[] { new ComparatorParameterDefinition("Key", UnitValueCategory.String) });
                return true;
            case "world.variables.getString":
                sourceId = UnitSourceId.WorldVariablesGetString;
                schema = new UnitSourceGetSchemaEntry("world.variables.getString", typeof(WorldVariableComponent), UnitValueCategory.String, new[] { new ComparatorParameterDefinition("Key", UnitValueCategory.String) });
                return true;
            case "world.variables.has":
                sourceId = UnitSourceId.WorldVariablesHas;
                schema = new UnitSourceGetSchemaEntry("world.variables.has", typeof(WorldVariableComponent), UnitValueCategory.Bool, new[] { new ComparatorParameterDefinition("Key", UnitValueCategory.String) });
                return true;
            default:
                sourceId = UnitSourceId.None;
                schema = default;
                return false;
        }
    }

    public static bool TryGetSet(string key, out UnitSourceId sourceId, out UnitSourceSetSchemaEntry schema)
    {
        switch (key)
        {
            case "player.skill.currentChainSlot.clear":
                sourceId = UnitSourceId.PlayerSkillCurrentChainSlotClear;
                schema = new UnitSourceSetSchemaEntry("player.skill.currentChainSlot.clear", typeof(PlayerCurrentSkillComponent), new[] { new ComparatorParameterDefinition("Clear", UnitValueCategory.Bool) }, false);
                return true;
            case "player.skill.currentChainSlot.set":
                sourceId = UnitSourceId.PlayerSkillCurrentChainSlotSet;
                schema = new UnitSourceSetSchemaEntry("player.skill.currentChainSlot.set", typeof(PlayerCurrentSkillComponent), new[] { new ComparatorParameterDefinition("ChainId", UnitValueCategory.Number), new ComparatorParameterDefinition("SlotIndex", UnitValueCategory.Number) }, false);
                return true;
            case "player.skill.pendingExtraModifiers.add":
                sourceId = UnitSourceId.PlayerSkillPendingExtraModifiersAdd;
                schema = new UnitSourceSetSchemaEntry("player.skill.pendingExtraModifiers.add", typeof(PlayerCurrentSkillComponent), new[] { new ComparatorParameterDefinition("Channel", UnitValueCategory.Number), new ComparatorParameterDefinition("Factor", UnitValueCategory.Number), new ComparatorParameterDefinition("Bonus", UnitValueCategory.Number) }, false);
                return true;
            case "unit.animation.play":
                sourceId = UnitSourceId.UnitAnimationPlay;
                schema = new UnitSourceSetSchemaEntry("unit.animation.play", typeof(UnitAnimationComponent), new[] { new ComparatorParameterDefinition("AnimationName", UnitValueCategory.String) }, false);
                return true;
            case "unit.animation.setName":
                sourceId = UnitSourceId.UnitAnimationSetName;
                schema = new UnitSourceSetSchemaEntry("unit.animation.setName", typeof(UnitAnimationComponent), new[] { new ComparatorParameterDefinition("AnimationName", UnitValueCategory.String) }, false);
                return true;
            case "unit.buffs.remove":
                sourceId = UnitSourceId.UnitBuffsRemove;
                schema = new UnitSourceSetSchemaEntry("unit.buffs.remove", typeof(UnitBuffComponent), new[] { new ComparatorParameterDefinition("BuffId", UnitValueCategory.Number) }, false);
                return true;
            case "unit.buffs.removeStacks":
                sourceId = UnitSourceId.UnitBuffsRemoveStacks;
                schema = new UnitSourceSetSchemaEntry("unit.buffs.removeStacks", typeof(UnitBuffComponent), new[] { new ComparatorParameterDefinition("BuffId", UnitValueCategory.Number), new ComparatorParameterDefinition("StackCount", UnitValueCategory.Number) }, false);
                return true;
            case "unit.facing.setDirection":
                sourceId = UnitSourceId.UnitFacingSetDirection;
                schema = new UnitSourceSetSchemaEntry("unit.facing.setDirection", typeof(UnitFacingComponent), new[] { new ComparatorParameterDefinition("Direction", UnitValueCategory.Float2) }, false);
                return true;
            case "unit.interestPoint.setNextPatrolTarget":
                sourceId = UnitSourceId.UnitInterestPointSetNextPatrolTarget;
                schema = new UnitSourceSetSchemaEntry("unit.interestPoint.setNextPatrolTarget", typeof(DungeonInterestPointComponent), new[] { new ComparatorParameterDefinition("Value", UnitValueCategory.Bool) }, false);
                return true;
            case "unit.interestPoint.setPatrolActive":
                sourceId = UnitSourceId.UnitInterestPointSetPatrolActive;
                schema = new UnitSourceSetSchemaEntry("unit.interestPoint.setPatrolActive", typeof(DungeonInterestPointComponent), new[] { new ComparatorParameterDefinition("Value", UnitValueCategory.Bool) }, false);
                return true;
            case "unit.interestPoint.setPatrolSpeed":
                sourceId = UnitSourceId.UnitInterestPointSetPatrolSpeed;
                schema = new UnitSourceSetSchemaEntry("unit.interestPoint.setPatrolSpeed", typeof(DungeonInterestPointComponent), new[] { new ComparatorParameterDefinition("Value", UnitValueCategory.Number) }, false);
                return true;
            case "unit.mana.cost":
                sourceId = UnitSourceId.UnitManaCost;
                schema = new UnitSourceSetSchemaEntry("unit.mana.cost", typeof(UnitManaComponent), new[] { new ComparatorParameterDefinition("Cost", UnitValueCategory.Number) }, false);
                return true;
            case "unit.move.setCommandSpeed":
                sourceId = UnitSourceId.UnitMoveSetCommandSpeed;
                schema = new UnitSourceSetSchemaEntry("unit.move.setCommandSpeed", typeof(UnitMoveComponent), new[] { new ComparatorParameterDefinition("Speed", UnitValueCategory.Number) }, false);
                return true;
            case "unit.move.setDirection":
                sourceId = UnitSourceId.UnitMoveSetDirection;
                schema = new UnitSourceSetSchemaEntry("unit.move.setDirection", typeof(UnitMoveComponent), new[] { new ComparatorParameterDefinition("Direction", UnitValueCategory.Float2) }, false);
                return true;
            case "unit.move.setFrameVelocity":
                sourceId = UnitSourceId.UnitMoveSetFrameVelocity;
                schema = new UnitSourceSetSchemaEntry("unit.move.setFrameVelocity", typeof(UnitMoveComponent), new[] { new ComparatorParameterDefinition("Velocity", UnitValueCategory.Float2) }, false);
                return true;
            case "unit.move.setStateMoveMultiplier":
                sourceId = UnitSourceId.UnitMoveSetStateMoveMultiplier;
                schema = new UnitSourceSetSchemaEntry("unit.move.setStateMoveMultiplier", typeof(UnitMoveComponent), new[] { new ComparatorParameterDefinition("Multiplier", UnitValueCategory.Number) }, false);
                return true;
            case "unit.move.setVelocity":
                sourceId = UnitSourceId.UnitMoveSetVelocity;
                schema = new UnitSourceSetSchemaEntry("unit.move.setVelocity", typeof(UnitMoveComponent), new[] { new ComparatorParameterDefinition("Velocity", UnitValueCategory.Float2) }, false);
                return true;
            case "unit.navigation.setDestination":
                sourceId = UnitSourceId.UnitNavigationSetDestination;
                schema = new UnitSourceSetSchemaEntry("unit.navigation.setDestination", typeof(UnitNavigationComponent), new[] { new ComparatorParameterDefinition("Destination", UnitValueCategory.Float3) }, false);
                return true;
            case "unit.navigation.stop":
                sourceId = UnitSourceId.UnitNavigationStop;
                schema = new UnitSourceSetSchemaEntry("unit.navigation.stop", typeof(UnitNavigationComponent), new[] { new ComparatorParameterDefinition("Ignored", UnitValueCategory.Any) }, false);
                return true;
            case "unit.variables.remove":
                sourceId = UnitSourceId.UnitVariablesRemove;
                schema = new UnitSourceSetSchemaEntry("unit.variables.remove", typeof(UnitVariableComponent), new[] { new ComparatorParameterDefinition("Key", UnitValueCategory.String) }, false);
                return true;
            case "unit.variables.set":
                sourceId = UnitSourceId.UnitVariablesSet;
                schema = new UnitSourceSetSchemaEntry("unit.variables.set", typeof(UnitVariableComponent), new[] { new ComparatorParameterDefinition("Value", UnitValueCategory.Any) }, true);
                return true;
            case "unit.variables.setOther":
                sourceId = UnitSourceId.UnitVariablesSetOther;
                schema = new UnitSourceSetSchemaEntry("unit.variables.setOther", typeof(UnitVariableComponent), new[] { new ComparatorParameterDefinition("Other", UnitValueCategory.Entity) }, false);
                return true;
            case "world.variables.remove":
                sourceId = UnitSourceId.WorldVariablesRemove;
                schema = new UnitSourceSetSchemaEntry("world.variables.remove", typeof(WorldVariableComponent), new[] { new ComparatorParameterDefinition("Key", UnitValueCategory.String) }, false);
                return true;
            case "world.variables.set":
                sourceId = UnitSourceId.WorldVariablesSet;
                schema = new UnitSourceSetSchemaEntry("world.variables.set", typeof(WorldVariableComponent), new[] { new ComparatorParameterDefinition("Value", UnitValueCategory.Any) }, true);
                return true;
            default:
                sourceId = UnitSourceId.None;
                schema = default;
                return false;
        }
    }

    public static UnitSourceSchema CreateSchema(GameObject prefab, bool includeAll)
    {
        UnitSourceSchemaBuilder builder = new();
        if (true)
        {
            builder.AddGet("game.interaction.hasCandidate", typeof(InteractionCandidateComponent), UnitValueCategory.Bool, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("game.interaction.candidateKind", typeof(InteractionCandidateComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("game.interaction.candidateTarget", typeof(InteractionCandidateComponent), UnitValueCategory.Entity, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("world.interaction.isInteracting", typeof(InteractionCandidateComponent), UnitValueCategory.Bool, Array.Empty<ComparatorParameterDefinition>());
            builder.AddInteractionGet(InteractionRequestKey, typeof(InteractionCandidateComponent));
        }
        if (includeAll || prefab != null && prefab.GetComponentInChildren(typeof(DungeonInterestPointAuthoring), true) != null)
        {
            builder.AddGet("unit.interestPoint.encounterId", typeof(DungeonInterestPointComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("unit.interestPoint.squadId", typeof(DungeonInterestPointComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("unit.interestPoint.spawnDistance", typeof(DungeonInterestPointComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("unit.interestPoint.patrolSpeed", typeof(DungeonInterestPointComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("unit.interestPoint.arrivalDistance", typeof(DungeonInterestPointComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("unit.interestPoint.hasPatrol", typeof(DungeonInterestPointComponent), UnitValueCategory.Bool, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("unit.interestPoint.currentTarget", typeof(DungeonInterestPointComponent), UnitValueCategory.Entity, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("unit.interestPoint.playerDistance", typeof(DungeonInterestPointComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("unit.interestPoint.shouldSpawnPatrol", typeof(DungeonInterestPointComponent), UnitValueCategory.Bool, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("unit.interestPoint.targetReached", typeof(DungeonInterestPointComponent), UnitValueCategory.Bool, Array.Empty<ComparatorParameterDefinition>());
            builder.AddSet("unit.interestPoint.setPatrolActive", typeof(DungeonInterestPointComponent), new[] { new ComparatorParameterDefinition("Value", UnitValueCategory.Bool) }, false);
            builder.AddSet("unit.interestPoint.setNextPatrolTarget", typeof(DungeonInterestPointComponent), new[] { new ComparatorParameterDefinition("Value", UnitValueCategory.Bool) }, false);
            builder.AddSet("unit.interestPoint.setPatrolSpeed", typeof(DungeonInterestPointComponent), new[] { new ComparatorParameterDefinition("Value", UnitValueCategory.Number) }, false);
        }
        if (includeAll || prefab != null && prefab.GetComponentInChildren(typeof(UnityEngine.Transform), true) != null)
        {
            builder.AddGet("unit.transform.position", typeof(LocalTransform), UnitValueCategory.Float3, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("unit.transform.forward", typeof(LocalTransform), UnitValueCategory.Float3, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("unit.transform.scale", typeof(LocalTransform), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("unit.transform.positionOf", typeof(LocalTransform), UnitValueCategory.Float3, new[] { new ComparatorParameterDefinition("Target Unit", UnitValueCategory.Entity) });
            builder.AddGet("unit.transform.directionTo", typeof(LocalTransform), UnitValueCategory.Float2, new[] { new ComparatorParameterDefinition("Target Unit", UnitValueCategory.Entity) });
        }
        if (includeAll || prefab != null && prefab.GetComponentInChildren(typeof(PlayerCurrentSkillAuthoring), true) != null)
        {
            builder.AddGet("player.skill.currentChainId", typeof(PlayerCurrentSkillComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("player.skill.currentSlotIndex", typeof(PlayerCurrentSkillComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("player.skill.currentSkillId", typeof(PlayerCurrentSkillComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("player.skill.currentInputType", typeof(PlayerCurrentSkillComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("player.skill.currentAdditionId", typeof(PlayerCurrentSkillComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("player.skill.hasCurrentSkill", typeof(PlayerCurrentSkillComponent), UnitValueCategory.Bool, Array.Empty<ComparatorParameterDefinition>());
            builder.AddSet("player.skill.currentChainSlot.set", typeof(PlayerCurrentSkillComponent), new[] { new ComparatorParameterDefinition("ChainId", UnitValueCategory.Number), new ComparatorParameterDefinition("SlotIndex", UnitValueCategory.Number) }, false);
            builder.AddSet("player.skill.currentChainSlot.clear", typeof(PlayerCurrentSkillComponent), new[] { new ComparatorParameterDefinition("Clear", UnitValueCategory.Bool) }, false);
            builder.AddSet("player.skill.pendingExtraModifiers.add", typeof(PlayerCurrentSkillComponent), new[] { new ComparatorParameterDefinition("Channel", UnitValueCategory.Number), new ComparatorParameterDefinition("Factor", UnitValueCategory.Number), new ComparatorParameterDefinition("Bonus", UnitValueCategory.Number) }, false);
        }
        if (includeAll || prefab != null && prefab.GetComponentInChildren(typeof(PlayerInputAuthoring), true) != null)
        {
            builder.AddGet("player.input.move", typeof(PlayerInputComponent), UnitValueCategory.Float2, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("player.input.pointerWorldPosition", typeof(PlayerInputComponent), UnitValueCategory.Float3, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("player.input.primaryHeld", typeof(PlayerInputComponent), UnitValueCategory.Bool, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("player.input.interactHeld", typeof(PlayerInputComponent), UnitValueCategory.Bool, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("player.input.inventoryHeld", typeof(PlayerInputComponent), UnitValueCategory.Bool, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("player.input.propertyHeld", typeof(PlayerInputComponent), UnitValueCategory.Bool, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("player.input.escapeHeld", typeof(PlayerInputComponent), UnitValueCategory.Bool, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("player.input.skillHeld", typeof(PlayerInputComponent), UnitValueCategory.Bool, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("player.input.skillChainIndex", typeof(PlayerInputComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("player.input.usePropHeld", typeof(PlayerInputComponent), UnitValueCategory.Bool, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("player.input.propIndex", typeof(PlayerInputComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
        }
        if (includeAll || prefab != null && prefab.GetComponentInChildren(typeof(PlayerCurrentSkillAuthoring), true) != null)
        {
            builder.AddGet("player.skill.isCurrentChainEmpty", typeof(PlayerInputComponent), UnitValueCategory.Bool, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("player.skill.getCurrentChainLength", typeof(PlayerInputComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("player.skill.getCurrentSkillId", typeof(PlayerInputComponent), UnitValueCategory.Number, new[] { new ComparatorParameterDefinition("Slot Index", UnitValueCategory.Number) });
            builder.AddGet("player.skill.getCurrentSkillAdditionId", typeof(PlayerInputComponent), UnitValueCategory.Number, new[] { new ComparatorParameterDefinition("Slot Index", UnitValueCategory.Number) });
            builder.AddGet("player.skill.hasCurrentSkillAt", typeof(PlayerInputComponent), UnitValueCategory.Bool, new[] { new ComparatorParameterDefinition("Slot Index", UnitValueCategory.Number) });
            builder.AddGet("player.skill.getCurrentSkillMpCost", typeof(PlayerInputComponent), UnitValueCategory.Number, new[] { new ComparatorParameterDefinition("Slot Index", UnitValueCategory.Number) });
            builder.AddGet("player.skill.getCurrentSkillChantDuration", typeof(PlayerInputComponent), UnitValueCategory.Number, new[] { new ComparatorParameterDefinition("Slot Index", UnitValueCategory.Number) });
            builder.AddGet("player.skill.getCurrentSkillRuntimeType", typeof(PlayerInputComponent), UnitValueCategory.String, new[] { new ComparatorParameterDefinition("Slot Index", UnitValueCategory.Number) });
            builder.AddGet("player.skill.isChainEmpty", typeof(PlayerInputComponent), UnitValueCategory.Bool, new[] { new ComparatorParameterDefinition("Chain ID", UnitValueCategory.Number) });
            builder.AddGet("player.skill.getChainLength", typeof(PlayerInputComponent), UnitValueCategory.Number, new[] { new ComparatorParameterDefinition("Chain ID", UnitValueCategory.Number) });
            builder.AddGet("player.skill.getChainSkillId", typeof(PlayerInputComponent), UnitValueCategory.Number, new[] { new ComparatorParameterDefinition("Chain ID", UnitValueCategory.Number), new ComparatorParameterDefinition("Slot Index", UnitValueCategory.Number) });
            builder.AddGet("player.skill.getChainSkillAdditionId", typeof(PlayerInputComponent), UnitValueCategory.Number, new[] { new ComparatorParameterDefinition("Chain ID", UnitValueCategory.Number), new ComparatorParameterDefinition("Slot Index", UnitValueCategory.Number) });
            builder.AddGet("player.skill.hasSkill", typeof(PlayerInputComponent), UnitValueCategory.Bool, new[] { new ComparatorParameterDefinition("Skill ID", UnitValueCategory.Number) });
            builder.AddGet("player.skill.getSkillMpCost", typeof(PlayerInputComponent), UnitValueCategory.Number, new[] { new ComparatorParameterDefinition("Skill ID", UnitValueCategory.Number) });
            builder.AddGet("player.skill.getSkillChantDuration", typeof(PlayerInputComponent), UnitValueCategory.Number, new[] { new ComparatorParameterDefinition("Skill ID", UnitValueCategory.Number) });
            builder.AddGet("player.skill.getSkillCastingMoveMultiplier", typeof(PlayerInputComponent), UnitValueCategory.Number, new[] { new ComparatorParameterDefinition("Skill ID", UnitValueCategory.Number) });
            builder.AddGet("player.skill.getSkillRuntimeType", typeof(PlayerInputComponent), UnitValueCategory.String, new[] { new ComparatorParameterDefinition("Skill ID", UnitValueCategory.Number) });
            builder.AddGet("player.skill.getInputType", typeof(PlayerInputComponent), UnitValueCategory.Number, new[] { new ComparatorParameterDefinition("Skill ID", UnitValueCategory.Number) });
        }
        if (includeAll || prefab != null && prefab.GetComponentInChildren(typeof(UnitAnimationAuthoring), true) != null)
        {
            builder.AddGet("unit.animation.name", typeof(UnitAnimationComponent), UnitValueCategory.String, Array.Empty<ComparatorParameterDefinition>());
            builder.AddSet("unit.animation.setName", typeof(UnitAnimationComponent), new[] { new ComparatorParameterDefinition("AnimationName", UnitValueCategory.String) }, false);
            builder.AddSet("unit.animation.play", typeof(UnitAnimationComponent), new[] { new ComparatorParameterDefinition("AnimationName", UnitValueCategory.String) }, false);
        }
        if (includeAll || prefab != null && prefab.GetComponentInChildren(typeof(UnitAttackAuthoring), true) != null)
        {
            builder.AddGet("unit.attack.baseAttackPower", typeof(UnitAttackComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("unit.attack.baseSkillRange", typeof(UnitAttackComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("unit.attack.baseChantSpeedBonus", typeof(UnitAttackComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("unit.attack.realAttackPower", typeof(UnitAttackComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("unit.attack.realSkillRange", typeof(UnitAttackComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("unit.attack.realChantSpeedBonus", typeof(UnitAttackComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("unit.attack.chantDurationMultiplier", typeof(UnitAttackComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
        }
        if (includeAll || prefab != null && prefab.GetComponentInChildren(typeof(UnitControlAuthoring), true) != null)
        {
            builder.AddGet("unit.control.entryCount", typeof(UnitControlRuntimeComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("unit.control.hasControl", typeof(UnitControlRuntimeComponent), UnitValueCategory.Bool, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("unit.control.activeType", typeof(UnitControlRuntimeComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("unit.control.activeRemainingTime", typeof(UnitControlRuntimeComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("unit.control.activePriority", typeof(UnitControlRuntimeComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("unit.control.lockMove", typeof(UnitControlRuntimeComponent), UnitValueCategory.Bool, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("unit.control.lockCast", typeof(UnitControlRuntimeComponent), UnitValueCategory.Bool, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("unit.control.activeSourceEntity", typeof(UnitControlRuntimeComponent), UnitValueCategory.Entity, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("unit.control.activeMotionVelocity", typeof(UnitControlRuntimeComponent), UnitValueCategory.Float2, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("unit.control.activeMotionDamping", typeof(UnitControlRuntimeComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("unit.control.entryTypeAt", typeof(UnitControlRuntimeComponent), UnitValueCategory.Number, new[] { new ComparatorParameterDefinition("Index", UnitValueCategory.Number) });
            builder.AddGet("unit.control.entryRemainingTimeAt", typeof(UnitControlRuntimeComponent), UnitValueCategory.Number, new[] { new ComparatorParameterDefinition("Index", UnitValueCategory.Number) });
            builder.AddGet("unit.control.entryPriorityAt", typeof(UnitControlRuntimeComponent), UnitValueCategory.Number, new[] { new ComparatorParameterDefinition("Index", UnitValueCategory.Number) });
            builder.AddGet("unit.control.entryLockMoveAt", typeof(UnitControlRuntimeComponent), UnitValueCategory.Bool, new[] { new ComparatorParameterDefinition("Index", UnitValueCategory.Number) });
            builder.AddGet("unit.control.entryLockCastAt", typeof(UnitControlRuntimeComponent), UnitValueCategory.Bool, new[] { new ComparatorParameterDefinition("Index", UnitValueCategory.Number) });
            builder.AddGet("unit.control.entryInterruptOnApplyAt", typeof(UnitControlRuntimeComponent), UnitValueCategory.Bool, new[] { new ComparatorParameterDefinition("Index", UnitValueCategory.Number) });
            builder.AddGet("unit.control.entrySourceEntityAt", typeof(UnitControlRuntimeComponent), UnitValueCategory.Entity, new[] { new ComparatorParameterDefinition("Index", UnitValueCategory.Number) });
            builder.AddGet("unit.control.entryMotionVelocityAt", typeof(UnitControlRuntimeComponent), UnitValueCategory.Float2, new[] { new ComparatorParameterDefinition("Index", UnitValueCategory.Number) });
            builder.AddGet("unit.control.entryMotionDampingAt", typeof(UnitControlRuntimeComponent), UnitValueCategory.Number, new[] { new ComparatorParameterDefinition("Index", UnitValueCategory.Number) });
        }
        if (includeAll || prefab != null && prefab.GetComponentInChildren(typeof(UnitBuffRuntimeAuthoring), true) != null)
        {
            builder.AddGet("unit.buffs.count", typeof(UnitBuffComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("unit.buffs.idAt", typeof(UnitBuffComponent), UnitValueCategory.Number, new[] { new ComparatorParameterDefinition("Index", UnitValueCategory.Number) });
            builder.AddGet("unit.buffs.remainingTimeAt", typeof(UnitBuffComponent), UnitValueCategory.Number, new[] { new ComparatorParameterDefinition("Index", UnitValueCategory.Number) });
            builder.AddGet("unit.buffs.stackCountAt", typeof(UnitBuffComponent), UnitValueCategory.Number, new[] { new ComparatorParameterDefinition("Index", UnitValueCategory.Number) });
            builder.AddGet("unit.buffs.hasOriginAt", typeof(UnitBuffComponent), UnitValueCategory.Bool, new[] { new ComparatorParameterDefinition("Index", UnitValueCategory.Number) });
            builder.AddGet("unit.buffs.originEntityAt", typeof(UnitBuffComponent), UnitValueCategory.Entity, new[] { new ComparatorParameterDefinition("Index", UnitValueCategory.Number) });
            builder.AddGet("unit.buffs.sourceSkillIdAt", typeof(UnitBuffComponent), UnitValueCategory.Number, new[] { new ComparatorParameterDefinition("Index", UnitValueCategory.Number) });
            builder.AddGet("unit.buffs.findIndex", typeof(UnitBuffComponent), UnitValueCategory.Number, new[] { new ComparatorParameterDefinition("BuffId", UnitValueCategory.Number) });
            builder.AddGet("unit.buffs.has", typeof(UnitBuffComponent), UnitValueCategory.Bool, new[] { new ComparatorParameterDefinition("BuffId", UnitValueCategory.Number) });
            builder.AddGet("unit.buffs.stackCount", typeof(UnitBuffComponent), UnitValueCategory.Number, new[] { new ComparatorParameterDefinition("BuffId", UnitValueCategory.Number) });
            builder.AddSet("unit.buffs.remove", typeof(UnitBuffComponent), new[] { new ComparatorParameterDefinition("BuffId", UnitValueCategory.Number) }, false);
            builder.AddSet("unit.buffs.removeStacks", typeof(UnitBuffComponent), new[] { new ComparatorParameterDefinition("BuffId", UnitValueCategory.Number), new ComparatorParameterDefinition("StackCount", UnitValueCategory.Number) }, false);
        }
        if (includeAll || prefab != null && prefab.GetComponentInChildren(typeof(UnitDeathAuthoring), true) != null)
        {
            builder.AddGet("unit.death.isActive", typeof(UnitDeathComponent), UnitValueCategory.Bool, Array.Empty<ComparatorParameterDefinition>());
        }
        if (includeAll || prefab != null && prefab.GetComponentInChildren(typeof(UnitFacingAuthoring), true) != null)
        {
            builder.AddGet("unit.facing.direction", typeof(UnitFacingComponent), UnitValueCategory.Float2, Array.Empty<ComparatorParameterDefinition>());
            builder.AddSet("unit.facing.setDirection", typeof(UnitFacingComponent), new[] { new ComparatorParameterDefinition("Direction", UnitValueCategory.Float2) }, false);
        }
        if (includeAll || prefab != null && prefab.GetComponentInChildren(typeof(UnitElementAuthoring), true) != null)
        {
            builder.AddGet("unit.element.waterPower", typeof(UnitElementComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("unit.element.firePower", typeof(UnitElementComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("unit.element.lightningPower", typeof(UnitElementComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("unit.element.windPower", typeof(UnitElementComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
        }
        if (includeAll || prefab != null && prefab.GetComponentInChildren(typeof(UnitFactionAuthoring), true) != null)
        {
            builder.AddGet("unit.faction.value", typeof(UnitFactionComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("unit.faction.isEnemyTo", typeof(UnitFactionComponent), UnitValueCategory.Bool, new[] { new ComparatorParameterDefinition("Other Entity", UnitValueCategory.Entity) });
        }
        if (includeAll || prefab != null && prefab.GetComponentInChildren(typeof(UnitManaAuthoring), true) != null)
        {
            builder.AddGet("unit.mana.baseMaxMp", typeof(UnitManaComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("unit.mana.baseMaxMpOffset", typeof(UnitManaComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("unit.mana.currentMana", typeof(UnitManaComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("unit.mana.baseMpRegenPerSecond", typeof(UnitManaComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("unit.mana.baseMpRegenPerSecondOffset", typeof(UnitManaComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("unit.mana.realMaxMp", typeof(UnitManaComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("unit.mana.currentManaPercentage", typeof(UnitManaComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("unit.mana.realMpRegenPerSecond", typeof(UnitManaComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
            builder.AddSet("unit.mana.cost", typeof(UnitManaComponent), new[] { new ComparatorParameterDefinition("Cost", UnitValueCategory.Number) }, false);
        }
        if (includeAll || prefab != null && prefab.GetComponentInChildren(typeof(UnitMoveAuthoring), true) != null)
        {
            builder.AddGet("unit.move.baseMoveSpeed", typeof(UnitMoveComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("unit.move.baseMaxAcceleration", typeof(UnitMoveComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("unit.move.direction", typeof(UnitMoveComponent), UnitValueCategory.Float2, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("unit.move.stateMoveMultiplier", typeof(UnitMoveComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("unit.move.commandSpeed", typeof(UnitMoveComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("unit.move.realMoveSpeed", typeof(UnitMoveComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("unit.move.realMaxAcceleration", typeof(UnitMoveComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
            builder.AddSet("unit.move.setDirection", typeof(UnitMoveComponent), new[] { new ComparatorParameterDefinition("Direction", UnitValueCategory.Float2) }, false);
            builder.AddSet("unit.move.setVelocity", typeof(UnitMoveComponent), new[] { new ComparatorParameterDefinition("Velocity", UnitValueCategory.Float2) }, false);
            builder.AddSet("unit.move.setFrameVelocity", typeof(UnitMoveComponent), new[] { new ComparatorParameterDefinition("Velocity", UnitValueCategory.Float2) }, false);
            builder.AddSet("unit.move.setStateMoveMultiplier", typeof(UnitMoveComponent), new[] { new ComparatorParameterDefinition("Multiplier", UnitValueCategory.Number) }, false);
            builder.AddSet("unit.move.setCommandSpeed", typeof(UnitMoveComponent), new[] { new ComparatorParameterDefinition("Speed", UnitValueCategory.Number) }, false);
        }
        if (includeAll || prefab != null && prefab.GetComponentInChildren(typeof(UnitNavigationAuthoring), true) != null)
        {
            builder.AddSet("unit.navigation.setDestination", typeof(UnitNavigationComponent), new[] { new ComparatorParameterDefinition("Destination", UnitValueCategory.Float3) }, false);
            builder.AddSet("unit.navigation.stop", typeof(UnitNavigationComponent), new[] { new ComparatorParameterDefinition("Ignored", UnitValueCategory.Any) }, false);
        }
        if (includeAll || prefab != null && prefab.GetComponentInChildren(typeof(UnitPerceptionAuthoring), true) != null)
        {
            builder.AddGet("unit.perception.searchRadius", typeof(UnitPerceptionComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("unit.perception.unitName", typeof(UnitPerceptionComponent), UnitValueCategory.String, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("unit.perception.unitCount", typeof(UnitPerceptionComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("unit.perception.unitAt", typeof(UnitPerceptionComponent), UnitValueCategory.Entity, new[] { new ComparatorParameterDefinition("Index", UnitValueCategory.Number) });
            builder.AddGet("unit.perception.unitsByFactionCount", typeof(UnitPerceptionComponent), UnitValueCategory.Number, new[] { new ComparatorParameterDefinition("Faction", UnitValueCategory.Number) });
            builder.AddGet("unit.perception.unitByFactionAt", typeof(UnitPerceptionComponent), UnitValueCategory.Entity, new[] { new ComparatorParameterDefinition("Faction", UnitValueCategory.Number), new ComparatorParameterDefinition("Index", UnitValueCategory.Number) });
            builder.AddGet("unit.perception.nearestUnitByFaction", typeof(UnitPerceptionComponent), UnitValueCategory.Entity, new[] { new ComparatorParameterDefinition("Faction", UnitValueCategory.Number) });
            builder.AddGet("unit.perception.unitsByNameCount", typeof(UnitPerceptionComponent), UnitValueCategory.Number, new[] { new ComparatorParameterDefinition("Unit Name", UnitValueCategory.String) });
            builder.AddGet("unit.perception.unitByNameAt", typeof(UnitPerceptionComponent), UnitValueCategory.Entity, new[] { new ComparatorParameterDefinition("Unit Name", UnitValueCategory.String), new ComparatorParameterDefinition("Index", UnitValueCategory.Number) });
            builder.AddGet("unit.perception.nearestUnitByName", typeof(UnitPerceptionComponent), UnitValueCategory.Entity, new[] { new ComparatorParameterDefinition("Unit Name", UnitValueCategory.String) });
        }
        if (includeAll || prefab != null && prefab.GetComponentInChildren(typeof(UnitSkillReleaseAuthoring), true) != null)
        {
            builder.AddGet("unit.self.entity", typeof(UnitSkillReleaseComponent), UnitValueCategory.Entity, Array.Empty<ComparatorParameterDefinition>());
        }
        if (includeAll || prefab != null && prefab.GetComponentInChildren(typeof(UnitVariableAuthoring), true) != null)
        {
            builder.AddGet("unit.variables.count", typeof(UnitVariableComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("unit.variables.consumerCount", typeof(UnitVariableComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("unit.variables.other", typeof(UnitVariableComponent), UnitValueCategory.Entity, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("unit.variables.has", typeof(UnitVariableComponent), UnitValueCategory.Bool, new[] { new ComparatorParameterDefinition("Key", UnitValueCategory.String) });
            builder.AddGet("unit.variables.get", typeof(UnitVariableComponent), UnitValueCategory.Any, new[] { new ComparatorParameterDefinition("Key", UnitValueCategory.String) });
            builder.AddGet("unit.variables.getNumber", typeof(UnitVariableComponent), UnitValueCategory.Number, new[] { new ComparatorParameterDefinition("Key", UnitValueCategory.String) });
            builder.AddGet("unit.variables.getBool", typeof(UnitVariableComponent), UnitValueCategory.Bool, new[] { new ComparatorParameterDefinition("Key", UnitValueCategory.String) });
            builder.AddGet("unit.variables.getFloat2", typeof(UnitVariableComponent), UnitValueCategory.Float2, new[] { new ComparatorParameterDefinition("Key", UnitValueCategory.String) });
            builder.AddGet("unit.variables.getFloat3", typeof(UnitVariableComponent), UnitValueCategory.Float3, new[] { new ComparatorParameterDefinition("Key", UnitValueCategory.String) });
            builder.AddGet("unit.variables.getEntity", typeof(UnitVariableComponent), UnitValueCategory.Entity, new[] { new ComparatorParameterDefinition("Key", UnitValueCategory.String) });
            builder.AddGet("unit.variables.getString", typeof(UnitVariableComponent), UnitValueCategory.String, new[] { new ComparatorParameterDefinition("Key", UnitValueCategory.String) });
            builder.AddSet("unit.variables.set", typeof(UnitVariableComponent), new[] { new ComparatorParameterDefinition("Value", UnitValueCategory.Any) }, true);
            builder.AddSet("unit.variables.remove", typeof(UnitVariableComponent), new[] { new ComparatorParameterDefinition("Key", UnitValueCategory.String) }, false);
            builder.AddSet("unit.variables.setOther", typeof(UnitVariableComponent), new[] { new ComparatorParameterDefinition("Other", UnitValueCategory.Entity) }, false);
        }
        if (includeAll || prefab != null && prefab.GetComponentInChildren(typeof(UnitVitalityAuthoring), true) != null)
        {
            builder.AddGet("unit.vitality.baseMaxHealth", typeof(UnitVitalityComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("unit.vitality.currentHealth", typeof(UnitVitalityComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("unit.vitality.baseHealthRegenPerSecond", typeof(UnitVitalityComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("unit.vitality.baseDefense", typeof(UnitVitalityComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("unit.vitality.realMaxHealth", typeof(UnitVitalityComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("unit.vitality.currentHealthPercentage", typeof(UnitVitalityComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("unit.vitality.realHealthRegenPerSecond", typeof(UnitVitalityComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("unit.vitality.realDefense", typeof(UnitVitalityComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
        }
        if (true)
        {
            builder.AddGet("world.variables.count", typeof(WorldVariableComponent), UnitValueCategory.Number, Array.Empty<ComparatorParameterDefinition>());
            builder.AddGet("world.variables.has", typeof(WorldVariableComponent), UnitValueCategory.Bool, new[] { new ComparatorParameterDefinition("Key", UnitValueCategory.String) });
            builder.AddGet("world.variables.get", typeof(WorldVariableComponent), UnitValueCategory.Any, new[] { new ComparatorParameterDefinition("Key", UnitValueCategory.String) });
            builder.AddGet("world.variables.getNumber", typeof(WorldVariableComponent), UnitValueCategory.Number, new[] { new ComparatorParameterDefinition("Key", UnitValueCategory.String) });
            builder.AddGet("world.variables.getBool", typeof(WorldVariableComponent), UnitValueCategory.Bool, new[] { new ComparatorParameterDefinition("Key", UnitValueCategory.String) });
            builder.AddGet("world.variables.getFloat2", typeof(WorldVariableComponent), UnitValueCategory.Float2, new[] { new ComparatorParameterDefinition("Key", UnitValueCategory.String) });
            builder.AddGet("world.variables.getFloat3", typeof(WorldVariableComponent), UnitValueCategory.Float3, new[] { new ComparatorParameterDefinition("Key", UnitValueCategory.String) });
            builder.AddGet("world.variables.getEntity", typeof(WorldVariableComponent), UnitValueCategory.Entity, new[] { new ComparatorParameterDefinition("Key", UnitValueCategory.String) });
            builder.AddGet("world.variables.getString", typeof(WorldVariableComponent), UnitValueCategory.String, new[] { new ComparatorParameterDefinition("Key", UnitValueCategory.String) });
            builder.AddSet("world.variables.set", typeof(WorldVariableComponent), new[] { new ComparatorParameterDefinition("Value", UnitValueCategory.Any) }, true);
            builder.AddSet("world.variables.remove", typeof(WorldVariableComponent), new[] { new ComparatorParameterDefinition("Key", UnitValueCategory.String) }, false);
        }
        return builder.Build();
    }

    public static UnitSourceSchema CreateSchema()
    {
        return CreateSchema(null, true);
    }
}

public static class UnitSourceSchemaFactory
{
    public static UnitSourceSchema CreateForAllSources()
    {
        return UnitComponentSourceRegistry.CreateSchema(null, true);
    }

    public static UnitSourceSchema CreateForPrefab(GameObject prefab)
    {
        return UnitComponentSourceRegistry.CreateSchema(prefab, false);
    }
}

public struct UnitSourceDispatcher
{
    private Entity _globalEntity;
    private ComponentLookup<DungeonInterestPointComponent> _DungeonInterestPointComponentLookup;
    private ComponentLookup<InteractionCandidateComponent> _InteractionCandidateComponentLookup;
    private ComponentLookup<LocalTransform> _LocalTransformLookup;
    private ComponentLookup<PlayerInputComponent> _PlayerInputComponentLookup;
    private ComponentLookup<PlayerCurrentSkillComponent> _PlayerCurrentSkillComponentLookup;
    private ComponentLookup<PlayerSkillDefinitionRegistryComponent> _PlayerSkillDefinitionRegistryComponentLookup;
    private ComponentLookup<UnitAnimationComponent> _UnitAnimationComponentLookup;
    private ComponentLookup<UnitAttackComponent> _UnitAttackComponentLookup;
    private ComponentLookup<UnitBuffComponent> _UnitBuffComponentLookup;
    private ComponentLookup<UnitControlRuntimeComponent> _UnitControlRuntimeComponentLookup;
    private ComponentLookup<DestroyEntityFlag> _DestroyEntityFlagLookup;
    private ComponentLookup<UnitDeathComponent> _UnitDeathComponentLookup;
    private ComponentLookup<UnitElementComponent> _UnitElementComponentLookup;
    private ComponentLookup<UnitFacingComponent> _UnitFacingComponentLookup;
    private ComponentLookup<UnitFactionComponent> _UnitFactionComponentLookup;
    private ComponentLookup<UnitManaComponent> _UnitManaComponentLookup;
    private ComponentLookup<UnitMoveComponent> _UnitMoveComponentLookup;
    private ComponentLookup<UnitModifierComponent> _UnitModifierComponentLookup;
    private ComponentLookup<UnitNavigationComponent> _UnitNavigationComponentLookup;
    private ComponentLookup<UnitPerceptionComponent> _UnitPerceptionComponentLookup;
    private ComponentLookup<UnitSkillReleaseComponent> _UnitSkillReleaseComponentLookup;
    private ComponentLookup<UnitVariableComponent> _UnitVariableComponentLookup;
    private ComponentLookup<UnitVitalityComponent> _UnitVitalityComponentLookup;
    private ComponentLookup<WorldStateComponent> _WorldStateComponentLookup;
    private ComponentLookup<WorldVariableComponent> _WorldVariableComponentLookup;
    private BufferLookup<UnitBuffElement> _UnitBuffElementBufferLookup;
    private BufferLookup<DungeonInterestPointCandidateElement> _DungeonInterestPointCandidateElementBufferLookup;
    private BufferLookup<PlayerSkillChainElement> _PlayerSkillChainElementBufferLookup;
    private BufferLookup<PlayerSkillChainSlotElement> _PlayerSkillChainSlotElementBufferLookup;
    private BufferLookup<UnitPerceptionUnitElement> _UnitPerceptionUnitElementBufferLookup;
    private BufferLookup<UnitVariableConsumerElement> _UnitVariableConsumerElementBufferLookup;
    private BufferLookup<UnitVariableElement> _UnitVariableElementBufferLookup;
    private BufferLookup<WorldVariableElement> _WorldVariableElementBufferLookup;

    public void Initialize(SystemBase system)
    {
        _DungeonInterestPointComponentLookup = system.GetComponentLookup<DungeonInterestPointComponent>(false);
        _InteractionCandidateComponentLookup = system.GetComponentLookup<InteractionCandidateComponent>(true);
        _LocalTransformLookup = system.GetComponentLookup<LocalTransform>(true);
        _PlayerInputComponentLookup = system.GetComponentLookup<PlayerInputComponent>(true);
        _PlayerCurrentSkillComponentLookup = system.GetComponentLookup<PlayerCurrentSkillComponent>(false);
        _PlayerSkillDefinitionRegistryComponentLookup = system.GetComponentLookup<PlayerSkillDefinitionRegistryComponent>(true);
        _UnitAnimationComponentLookup = system.GetComponentLookup<UnitAnimationComponent>(false);
        _UnitAttackComponentLookup = system.GetComponentLookup<UnitAttackComponent>(true);
        _UnitBuffComponentLookup = system.GetComponentLookup<UnitBuffComponent>(false);
        _UnitControlRuntimeComponentLookup = system.GetComponentLookup<UnitControlRuntimeComponent>(true);
        _DestroyEntityFlagLookup = system.GetComponentLookup<DestroyEntityFlag>(true);
        _UnitDeathComponentLookup = system.GetComponentLookup<UnitDeathComponent>(true);
        _UnitElementComponentLookup = system.GetComponentLookup<UnitElementComponent>(true);
        _UnitFacingComponentLookup = system.GetComponentLookup<UnitFacingComponent>(false);
        _UnitFactionComponentLookup = system.GetComponentLookup<UnitFactionComponent>(true);
        _UnitManaComponentLookup = system.GetComponentLookup<UnitManaComponent>(false);
        _UnitMoveComponentLookup = system.GetComponentLookup<UnitMoveComponent>(false);
        _UnitModifierComponentLookup = system.GetComponentLookup<UnitModifierComponent>(true);
        _UnitNavigationComponentLookup = system.GetComponentLookup<UnitNavigationComponent>(false);
        _UnitPerceptionComponentLookup = system.GetComponentLookup<UnitPerceptionComponent>(true);
        _UnitSkillReleaseComponentLookup = system.GetComponentLookup<UnitSkillReleaseComponent>(true);
        _UnitVariableComponentLookup = system.GetComponentLookup<UnitVariableComponent>(false);
        _UnitVitalityComponentLookup = system.GetComponentLookup<UnitVitalityComponent>(true);
        _WorldStateComponentLookup = system.GetComponentLookup<WorldStateComponent>(true);
        _WorldVariableComponentLookup = system.GetComponentLookup<WorldVariableComponent>(true);
        _UnitBuffElementBufferLookup = system.GetBufferLookup<UnitBuffElement>(false);
        _DungeonInterestPointCandidateElementBufferLookup = system.GetBufferLookup<DungeonInterestPointCandidateElement>(true);
        _PlayerSkillChainElementBufferLookup = system.GetBufferLookup<PlayerSkillChainElement>(true);
        _PlayerSkillChainSlotElementBufferLookup = system.GetBufferLookup<PlayerSkillChainSlotElement>(true);
        _UnitPerceptionUnitElementBufferLookup = system.GetBufferLookup<UnitPerceptionUnitElement>(true);
        _UnitVariableConsumerElementBufferLookup = system.GetBufferLookup<UnitVariableConsumerElement>(false);
        _UnitVariableElementBufferLookup = system.GetBufferLookup<UnitVariableElement>(false);
        _WorldVariableElementBufferLookup = system.GetBufferLookup<WorldVariableElement>(false);
        _globalEntity = WorldStateUtility.GetEntity(system.EntityManager);
    }

    public void InitializeReadOnly(SystemBase system)
    {
        _DungeonInterestPointComponentLookup = system.GetComponentLookup<DungeonInterestPointComponent>(true);
        _InteractionCandidateComponentLookup = system.GetComponentLookup<InteractionCandidateComponent>(true);
        _LocalTransformLookup = system.GetComponentLookup<LocalTransform>(true);
        _PlayerInputComponentLookup = system.GetComponentLookup<PlayerInputComponent>(true);
        _PlayerCurrentSkillComponentLookup = system.GetComponentLookup<PlayerCurrentSkillComponent>(true);
        _PlayerSkillDefinitionRegistryComponentLookup = system.GetComponentLookup<PlayerSkillDefinitionRegistryComponent>(true);
        _UnitAnimationComponentLookup = system.GetComponentLookup<UnitAnimationComponent>(true);
        _UnitAttackComponentLookup = system.GetComponentLookup<UnitAttackComponent>(true);
        _UnitBuffComponentLookup = system.GetComponentLookup<UnitBuffComponent>(true);
        _UnitControlRuntimeComponentLookup = system.GetComponentLookup<UnitControlRuntimeComponent>(true);
        _DestroyEntityFlagLookup = system.GetComponentLookup<DestroyEntityFlag>(true);
        _UnitDeathComponentLookup = system.GetComponentLookup<UnitDeathComponent>(true);
        _UnitElementComponentLookup = system.GetComponentLookup<UnitElementComponent>(true);
        _UnitFacingComponentLookup = system.GetComponentLookup<UnitFacingComponent>(true);
        _UnitFactionComponentLookup = system.GetComponentLookup<UnitFactionComponent>(true);
        _UnitManaComponentLookup = system.GetComponentLookup<UnitManaComponent>(true);
        _UnitMoveComponentLookup = system.GetComponentLookup<UnitMoveComponent>(true);
        _UnitModifierComponentLookup = system.GetComponentLookup<UnitModifierComponent>(true);
        _UnitNavigationComponentLookup = system.GetComponentLookup<UnitNavigationComponent>(true);
        _UnitPerceptionComponentLookup = system.GetComponentLookup<UnitPerceptionComponent>(true);
        _UnitSkillReleaseComponentLookup = system.GetComponentLookup<UnitSkillReleaseComponent>(true);
        _UnitVariableComponentLookup = system.GetComponentLookup<UnitVariableComponent>(true);
        _UnitVitalityComponentLookup = system.GetComponentLookup<UnitVitalityComponent>(true);
        _WorldStateComponentLookup = system.GetComponentLookup<WorldStateComponent>(true);
        _WorldVariableComponentLookup = system.GetComponentLookup<WorldVariableComponent>(true);
        _UnitBuffElementBufferLookup = system.GetBufferLookup<UnitBuffElement>(true);
        _DungeonInterestPointCandidateElementBufferLookup = system.GetBufferLookup<DungeonInterestPointCandidateElement>(true);
        _PlayerSkillChainElementBufferLookup = system.GetBufferLookup<PlayerSkillChainElement>(true);
        _PlayerSkillChainSlotElementBufferLookup = system.GetBufferLookup<PlayerSkillChainSlotElement>(true);
        _UnitPerceptionUnitElementBufferLookup = system.GetBufferLookup<UnitPerceptionUnitElement>(true);
        _UnitVariableConsumerElementBufferLookup = system.GetBufferLookup<UnitVariableConsumerElement>(true);
        _UnitVariableElementBufferLookup = system.GetBufferLookup<UnitVariableElement>(true);
        _WorldVariableElementBufferLookup = system.GetBufferLookup<WorldVariableElement>(true);
        _globalEntity = WorldStateUtility.GetEntity(system.EntityManager);
    }

    public void Update(SystemBase system)
    {
        _DungeonInterestPointComponentLookup.Update(system);
        _InteractionCandidateComponentLookup.Update(system);
        _LocalTransformLookup.Update(system);
        _PlayerInputComponentLookup.Update(system);
        _PlayerCurrentSkillComponentLookup.Update(system);
        _PlayerSkillDefinitionRegistryComponentLookup.Update(system);
        _UnitAnimationComponentLookup.Update(system);
        _UnitAttackComponentLookup.Update(system);
        _UnitBuffComponentLookup.Update(system);
        _UnitControlRuntimeComponentLookup.Update(system);
        _DestroyEntityFlagLookup.Update(system);
        _UnitDeathComponentLookup.Update(system);
        _UnitElementComponentLookup.Update(system);
        _UnitFacingComponentLookup.Update(system);
        _UnitFactionComponentLookup.Update(system);
        _UnitManaComponentLookup.Update(system);
        _UnitMoveComponentLookup.Update(system);
        _UnitModifierComponentLookup.Update(system);
        _UnitNavigationComponentLookup.Update(system);
        _UnitPerceptionComponentLookup.Update(system);
        _UnitSkillReleaseComponentLookup.Update(system);
        _UnitVariableComponentLookup.Update(system);
        _UnitVitalityComponentLookup.Update(system);
        _WorldStateComponentLookup.Update(system);
        _WorldVariableComponentLookup.Update(system);
        _UnitBuffElementBufferLookup.Update(system);
        _DungeonInterestPointCandidateElementBufferLookup.Update(system);
        _PlayerSkillChainElementBufferLookup.Update(system);
        _PlayerSkillChainSlotElementBufferLookup.Update(system);
        _UnitPerceptionUnitElementBufferLookup.Update(system);
        _UnitVariableConsumerElementBufferLookup.Update(system);
        _UnitVariableElementBufferLookup.Update(system);
        _WorldVariableElementBufferLookup.Update(system);
    }

    public void Initialize(ref SystemState state)
    {
        _DungeonInterestPointComponentLookup = state.GetComponentLookup<DungeonInterestPointComponent>(false);
        _InteractionCandidateComponentLookup = state.GetComponentLookup<InteractionCandidateComponent>(true);
        _LocalTransformLookup = state.GetComponentLookup<LocalTransform>(true);
        _PlayerInputComponentLookup = state.GetComponentLookup<PlayerInputComponent>(true);
        _PlayerCurrentSkillComponentLookup = state.GetComponentLookup<PlayerCurrentSkillComponent>(false);
        _PlayerSkillDefinitionRegistryComponentLookup = state.GetComponentLookup<PlayerSkillDefinitionRegistryComponent>(true);
        _UnitAnimationComponentLookup = state.GetComponentLookup<UnitAnimationComponent>(false);
        _UnitAttackComponentLookup = state.GetComponentLookup<UnitAttackComponent>(true);
        _UnitBuffComponentLookup = state.GetComponentLookup<UnitBuffComponent>(false);
        _UnitControlRuntimeComponentLookup = state.GetComponentLookup<UnitControlRuntimeComponent>(true);
        _DestroyEntityFlagLookup = state.GetComponentLookup<DestroyEntityFlag>(true);
        _UnitDeathComponentLookup = state.GetComponentLookup<UnitDeathComponent>(true);
        _UnitElementComponentLookup = state.GetComponentLookup<UnitElementComponent>(true);
        _UnitFacingComponentLookup = state.GetComponentLookup<UnitFacingComponent>(false);
        _UnitFactionComponentLookup = state.GetComponentLookup<UnitFactionComponent>(true);
        _UnitManaComponentLookup = state.GetComponentLookup<UnitManaComponent>(false);
        _UnitMoveComponentLookup = state.GetComponentLookup<UnitMoveComponent>(false);
        _UnitModifierComponentLookup = state.GetComponentLookup<UnitModifierComponent>(true);
        _UnitNavigationComponentLookup = state.GetComponentLookup<UnitNavigationComponent>(false);
        _UnitPerceptionComponentLookup = state.GetComponentLookup<UnitPerceptionComponent>(true);
        _UnitSkillReleaseComponentLookup = state.GetComponentLookup<UnitSkillReleaseComponent>(true);
        _UnitVariableComponentLookup = state.GetComponentLookup<UnitVariableComponent>(false);
        _UnitVitalityComponentLookup = state.GetComponentLookup<UnitVitalityComponent>(true);
        _WorldStateComponentLookup = state.GetComponentLookup<WorldStateComponent>(true);
        _WorldVariableComponentLookup = state.GetComponentLookup<WorldVariableComponent>(true);
        _UnitBuffElementBufferLookup = state.GetBufferLookup<UnitBuffElement>(false);
        _DungeonInterestPointCandidateElementBufferLookup = state.GetBufferLookup<DungeonInterestPointCandidateElement>(true);
        _PlayerSkillChainElementBufferLookup = state.GetBufferLookup<PlayerSkillChainElement>(true);
        _PlayerSkillChainSlotElementBufferLookup = state.GetBufferLookup<PlayerSkillChainSlotElement>(true);
        _UnitPerceptionUnitElementBufferLookup = state.GetBufferLookup<UnitPerceptionUnitElement>(true);
        _UnitVariableConsumerElementBufferLookup = state.GetBufferLookup<UnitVariableConsumerElement>(false);
        _UnitVariableElementBufferLookup = state.GetBufferLookup<UnitVariableElement>(false);
        _WorldVariableElementBufferLookup = state.GetBufferLookup<WorldVariableElement>(false);
        _globalEntity = WorldStateUtility.GetEntity(state.EntityManager);
    }

    public void InitializeReadOnly(ref SystemState state)
    {
        _DungeonInterestPointComponentLookup = state.GetComponentLookup<DungeonInterestPointComponent>(true);
        _InteractionCandidateComponentLookup = state.GetComponentLookup<InteractionCandidateComponent>(true);
        _LocalTransformLookup = state.GetComponentLookup<LocalTransform>(true);
        _PlayerInputComponentLookup = state.GetComponentLookup<PlayerInputComponent>(true);
        _PlayerCurrentSkillComponentLookup = state.GetComponentLookup<PlayerCurrentSkillComponent>(true);
        _PlayerSkillDefinitionRegistryComponentLookup = state.GetComponentLookup<PlayerSkillDefinitionRegistryComponent>(true);
        _UnitAnimationComponentLookup = state.GetComponentLookup<UnitAnimationComponent>(true);
        _UnitAttackComponentLookup = state.GetComponentLookup<UnitAttackComponent>(true);
        _UnitBuffComponentLookup = state.GetComponentLookup<UnitBuffComponent>(true);
        _UnitControlRuntimeComponentLookup = state.GetComponentLookup<UnitControlRuntimeComponent>(true);
        _DestroyEntityFlagLookup = state.GetComponentLookup<DestroyEntityFlag>(true);
        _UnitDeathComponentLookup = state.GetComponentLookup<UnitDeathComponent>(true);
        _UnitElementComponentLookup = state.GetComponentLookup<UnitElementComponent>(true);
        _UnitFacingComponentLookup = state.GetComponentLookup<UnitFacingComponent>(true);
        _UnitFactionComponentLookup = state.GetComponentLookup<UnitFactionComponent>(true);
        _UnitManaComponentLookup = state.GetComponentLookup<UnitManaComponent>(true);
        _UnitMoveComponentLookup = state.GetComponentLookup<UnitMoveComponent>(true);
        _UnitModifierComponentLookup = state.GetComponentLookup<UnitModifierComponent>(true);
        _UnitNavigationComponentLookup = state.GetComponentLookup<UnitNavigationComponent>(true);
        _UnitPerceptionComponentLookup = state.GetComponentLookup<UnitPerceptionComponent>(true);
        _UnitSkillReleaseComponentLookup = state.GetComponentLookup<UnitSkillReleaseComponent>(true);
        _UnitVariableComponentLookup = state.GetComponentLookup<UnitVariableComponent>(true);
        _UnitVitalityComponentLookup = state.GetComponentLookup<UnitVitalityComponent>(true);
        _WorldStateComponentLookup = state.GetComponentLookup<WorldStateComponent>(true);
        _WorldVariableComponentLookup = state.GetComponentLookup<WorldVariableComponent>(true);
        _UnitBuffElementBufferLookup = state.GetBufferLookup<UnitBuffElement>(true);
        _DungeonInterestPointCandidateElementBufferLookup = state.GetBufferLookup<DungeonInterestPointCandidateElement>(true);
        _PlayerSkillChainElementBufferLookup = state.GetBufferLookup<PlayerSkillChainElement>(true);
        _PlayerSkillChainSlotElementBufferLookup = state.GetBufferLookup<PlayerSkillChainSlotElement>(true);
        _UnitPerceptionUnitElementBufferLookup = state.GetBufferLookup<UnitPerceptionUnitElement>(true);
        _UnitVariableConsumerElementBufferLookup = state.GetBufferLookup<UnitVariableConsumerElement>(true);
        _UnitVariableElementBufferLookup = state.GetBufferLookup<UnitVariableElement>(true);
        _WorldVariableElementBufferLookup = state.GetBufferLookup<WorldVariableElement>(true);
        _globalEntity = WorldStateUtility.GetEntity(state.EntityManager);
    }

    public void Update(ref SystemState state)
    {
        _DungeonInterestPointComponentLookup.Update(ref state);
        _InteractionCandidateComponentLookup.Update(ref state);
        _LocalTransformLookup.Update(ref state);
        _PlayerInputComponentLookup.Update(ref state);
        _PlayerCurrentSkillComponentLookup.Update(ref state);
        _PlayerSkillDefinitionRegistryComponentLookup.Update(ref state);
        _UnitAnimationComponentLookup.Update(ref state);
        _UnitAttackComponentLookup.Update(ref state);
        _UnitBuffComponentLookup.Update(ref state);
        _UnitControlRuntimeComponentLookup.Update(ref state);
        _DestroyEntityFlagLookup.Update(ref state);
        _UnitDeathComponentLookup.Update(ref state);
        _UnitElementComponentLookup.Update(ref state);
        _UnitFacingComponentLookup.Update(ref state);
        _UnitFactionComponentLookup.Update(ref state);
        _UnitManaComponentLookup.Update(ref state);
        _UnitMoveComponentLookup.Update(ref state);
        _UnitModifierComponentLookup.Update(ref state);
        _UnitNavigationComponentLookup.Update(ref state);
        _UnitPerceptionComponentLookup.Update(ref state);
        _UnitSkillReleaseComponentLookup.Update(ref state);
        _UnitVariableComponentLookup.Update(ref state);
        _UnitVitalityComponentLookup.Update(ref state);
        _WorldStateComponentLookup.Update(ref state);
        _WorldVariableComponentLookup.Update(ref state);
        _UnitBuffElementBufferLookup.Update(ref state);
        _DungeonInterestPointCandidateElementBufferLookup.Update(ref state);
        _PlayerSkillChainElementBufferLookup.Update(ref state);
        _PlayerSkillChainSlotElementBufferLookup.Update(ref state);
        _UnitPerceptionUnitElementBufferLookup.Update(ref state);
        _UnitVariableConsumerElementBufferLookup.Update(ref state);
        _UnitVariableElementBufferLookup.Update(ref state);
        _WorldVariableElementBufferLookup.Update(ref state);
    }

    public bool TryGet(Entity entity, UnitSourceId sourceId, in UnitSourceArguments arguments, out UnitSourceValue value)
    {
        value = default;
        switch (sourceId)
        {
            case UnitSourceId.GameInteractionCandidateKind:
            {
                if (!_InteractionCandidateComponentLookup.TryGetComponent(_globalEntity, out InteractionCandidateComponent component))
                    return false;
                return GameInteractionSource.TryGet(1, in component, in arguments, out value);
            }
            case UnitSourceId.GameInteractionCandidateTarget:
            {
                if (!_InteractionCandidateComponentLookup.TryGetComponent(_globalEntity, out InteractionCandidateComponent component))
                    return false;
                return GameInteractionSource.TryGet(2, in component, in arguments, out value);
            }
            case UnitSourceId.GameInteractionHasCandidate:
            {
                if (!_InteractionCandidateComponentLookup.TryGetComponent(_globalEntity, out InteractionCandidateComponent component))
                    return false;
                return GameInteractionSource.TryGet(0, in component, in arguments, out value);
            }
            case UnitSourceId.PlayerInputEscapeHeld:
            {
                if (!_PlayerInputComponentLookup.TryGetComponent(entity, out PlayerInputComponent component))
                    return false;
                return PlayerInputSource.TryGet(6, in component, in arguments, out value);
            }
            case UnitSourceId.PlayerInputInteractHeld:
            {
                if (!_PlayerInputComponentLookup.TryGetComponent(entity, out PlayerInputComponent component))
                    return false;
                return PlayerInputSource.TryGet(3, in component, in arguments, out value);
            }
            case UnitSourceId.PlayerInputInventoryHeld:
            {
                if (!_PlayerInputComponentLookup.TryGetComponent(entity, out PlayerInputComponent component))
                    return false;
                return PlayerInputSource.TryGet(4, in component, in arguments, out value);
            }
            case UnitSourceId.PlayerInputMove:
            {
                if (!_PlayerInputComponentLookup.TryGetComponent(entity, out PlayerInputComponent component))
                    return false;
                return PlayerInputSource.TryGet(0, in component, in arguments, out value);
            }
            case UnitSourceId.PlayerInputPointerWorldPosition:
            {
                if (!_PlayerInputComponentLookup.TryGetComponent(entity, out PlayerInputComponent component))
                    return false;
                return PlayerInputSource.TryGet(1, in component, in arguments, out value);
            }
            case UnitSourceId.PlayerInputPrimaryHeld:
            {
                if (!_PlayerInputComponentLookup.TryGetComponent(entity, out PlayerInputComponent component))
                    return false;
                return PlayerInputSource.TryGet(2, in component, in arguments, out value);
            }
            case UnitSourceId.PlayerInputPropertyHeld:
            {
                if (!_PlayerInputComponentLookup.TryGetComponent(entity, out PlayerInputComponent component))
                    return false;
                return PlayerInputSource.TryGet(5, in component, in arguments, out value);
            }
            case UnitSourceId.PlayerInputPropIndex:
            {
                if (!_PlayerInputComponentLookup.TryGetComponent(entity, out PlayerInputComponent component))
                    return false;
                return PlayerInputSource.TryGet(11, in component, in arguments, out value);
            }
            case UnitSourceId.PlayerInputSkillChainIndex:
            {
                if (!_PlayerInputComponentLookup.TryGetComponent(entity, out PlayerInputComponent component))
                    return false;
                return PlayerInputSource.TryGet(8, in component, in arguments, out value);
            }
            case UnitSourceId.PlayerInputSkillHeld:
            {
                if (!_PlayerInputComponentLookup.TryGetComponent(entity, out PlayerInputComponent component))
                    return false;
                return PlayerInputSource.TryGet(7, in component, in arguments, out value);
            }
            case UnitSourceId.PlayerInputUsePropHeld:
            {
                if (!_PlayerInputComponentLookup.TryGetComponent(entity, out PlayerInputComponent component))
                    return false;
                return PlayerInputSource.TryGet(10, in component, in arguments, out value);
            }
            case UnitSourceId.PlayerSkillCurrentChainId:
            {
                if (!_PlayerCurrentSkillComponentLookup.TryGetComponent(entity, out PlayerCurrentSkillComponent component))
                    return false;
                return PlayerCurrentSkillSource.TryGetStored(0, in component, in arguments, out value);
            }
            case UnitSourceId.PlayerSkillCurrentSlotIndex:
            {
                if (!_PlayerCurrentSkillComponentLookup.TryGetComponent(entity, out PlayerCurrentSkillComponent component))
                    return false;
                return PlayerCurrentSkillSource.TryGetStored(1, in component, in arguments, out value);
            }
            case UnitSourceId.UnitAnimationName:
            {
                if (!_UnitAnimationComponentLookup.TryGetComponent(entity, out UnitAnimationComponent component))
                    return false;
                return UnitAnimationSource.TryGet(0, in component, in arguments, out value);
            }
            case UnitSourceId.UnitAttackBaseAttackPower:
            {
                if (!_UnitAttackComponentLookup.TryGetComponent(entity, out UnitAttackComponent component))
                    return false;
                return UnitAttackSource.TryGet(0, in component, in arguments, out value);
            }
            case UnitSourceId.UnitAttackBaseChantSpeedBonus:
            {
                if (!_UnitAttackComponentLookup.TryGetComponent(entity, out UnitAttackComponent component))
                    return false;
                return UnitAttackSource.TryGet(2, in component, in arguments, out value);
            }
            case UnitSourceId.UnitAttackBaseSkillRange:
            {
                if (!_UnitAttackComponentLookup.TryGetComponent(entity, out UnitAttackComponent component))
                    return false;
                return UnitAttackSource.TryGet(1, in component, in arguments, out value);
            }
            case UnitSourceId.UnitControlActiveMotionDamping:
            {
                if (!_UnitControlRuntimeComponentLookup.TryGetComponent(entity, out UnitControlRuntimeComponent component))
                    return false;
                return UnitControlSource.TryGet(9, in component, in arguments, out value);
            }
            case UnitSourceId.UnitControlActiveMotionVelocity:
            {
                if (!_UnitControlRuntimeComponentLookup.TryGetComponent(entity, out UnitControlRuntimeComponent component))
                    return false;
                return UnitControlSource.TryGet(8, in component, in arguments, out value);
            }
            case UnitSourceId.UnitControlActivePriority:
            {
                if (!_UnitControlRuntimeComponentLookup.TryGetComponent(entity, out UnitControlRuntimeComponent component))
                    return false;
                return UnitControlSource.TryGet(4, in component, in arguments, out value);
            }
            case UnitSourceId.UnitControlActiveRemainingTime:
            {
                if (!_UnitControlRuntimeComponentLookup.TryGetComponent(entity, out UnitControlRuntimeComponent component))
                    return false;
                return UnitControlSource.TryGet(3, in component, in arguments, out value);
            }
            case UnitSourceId.UnitControlActiveSourceEntity:
            {
                if (!_UnitControlRuntimeComponentLookup.TryGetComponent(entity, out UnitControlRuntimeComponent component))
                    return false;
                return UnitControlSource.TryGet(7, in component, in arguments, out value);
            }
            case UnitSourceId.UnitControlActiveType:
            {
                if (!_UnitControlRuntimeComponentLookup.TryGetComponent(entity, out UnitControlRuntimeComponent component))
                    return false;
                return UnitControlSource.TryGet(2, in component, in arguments, out value);
            }
            case UnitSourceId.UnitControlEntryCount:
            {
                if (!_UnitControlRuntimeComponentLookup.TryGetComponent(entity, out UnitControlRuntimeComponent component))
                    return false;
                return UnitControlSource.TryGet(0, in component, in arguments, out value);
            }
            case UnitSourceId.UnitControlEntryInterruptOnApplyAt:
            {
                if (!_UnitControlRuntimeComponentLookup.TryGetComponent(entity, out UnitControlRuntimeComponent component))
                    return false;
                return UnitControlSource.TryGetEntry(15, in component, in arguments, out value);
            }
            case UnitSourceId.UnitControlEntryLockCastAt:
            {
                if (!_UnitControlRuntimeComponentLookup.TryGetComponent(entity, out UnitControlRuntimeComponent component))
                    return false;
                return UnitControlSource.TryGetEntry(14, in component, in arguments, out value);
            }
            case UnitSourceId.UnitControlEntryLockMoveAt:
            {
                if (!_UnitControlRuntimeComponentLookup.TryGetComponent(entity, out UnitControlRuntimeComponent component))
                    return false;
                return UnitControlSource.TryGetEntry(13, in component, in arguments, out value);
            }
            case UnitSourceId.UnitControlEntryMotionDampingAt:
            {
                if (!_UnitControlRuntimeComponentLookup.TryGetComponent(entity, out UnitControlRuntimeComponent component))
                    return false;
                return UnitControlSource.TryGetEntry(18, in component, in arguments, out value);
            }
            case UnitSourceId.UnitControlEntryMotionVelocityAt:
            {
                if (!_UnitControlRuntimeComponentLookup.TryGetComponent(entity, out UnitControlRuntimeComponent component))
                    return false;
                return UnitControlSource.TryGetEntry(17, in component, in arguments, out value);
            }
            case UnitSourceId.UnitControlEntryPriorityAt:
            {
                if (!_UnitControlRuntimeComponentLookup.TryGetComponent(entity, out UnitControlRuntimeComponent component))
                    return false;
                return UnitControlSource.TryGetEntry(12, in component, in arguments, out value);
            }
            case UnitSourceId.UnitControlEntryRemainingTimeAt:
            {
                if (!_UnitControlRuntimeComponentLookup.TryGetComponent(entity, out UnitControlRuntimeComponent component))
                    return false;
                return UnitControlSource.TryGetEntry(11, in component, in arguments, out value);
            }
            case UnitSourceId.UnitControlEntrySourceEntityAt:
            {
                if (!_UnitControlRuntimeComponentLookup.TryGetComponent(entity, out UnitControlRuntimeComponent component))
                    return false;
                return UnitControlSource.TryGetEntry(16, in component, in arguments, out value);
            }
            case UnitSourceId.UnitControlEntryTypeAt:
            {
                if (!_UnitControlRuntimeComponentLookup.TryGetComponent(entity, out UnitControlRuntimeComponent component))
                    return false;
                return UnitControlSource.TryGetEntry(10, in component, in arguments, out value);
            }
            case UnitSourceId.UnitControlHasControl:
            {
                if (!_UnitControlRuntimeComponentLookup.TryGetComponent(entity, out UnitControlRuntimeComponent component))
                    return false;
                return UnitControlSource.TryGet(1, in component, in arguments, out value);
            }
            case UnitSourceId.UnitControlLockCast:
            {
                if (!_UnitControlRuntimeComponentLookup.TryGetComponent(entity, out UnitControlRuntimeComponent component))
                    return false;
                return UnitControlSource.TryGet(6, in component, in arguments, out value);
            }
            case UnitSourceId.UnitControlLockMove:
            {
                if (!_UnitControlRuntimeComponentLookup.TryGetComponent(entity, out UnitControlRuntimeComponent component))
                    return false;
                return UnitControlSource.TryGet(5, in component, in arguments, out value);
            }
            case UnitSourceId.UnitFacingDirection:
            {
                if (!_UnitFacingComponentLookup.TryGetComponent(entity, out UnitFacingComponent component))
                    return false;
                return UnitFacingSource.TryGet(0, in component, in arguments, out value);
            }
            case UnitSourceId.UnitFactionValue:
            {
                if (!_UnitFactionComponentLookup.TryGetComponent(entity, out UnitFactionComponent component))
                    return false;
                return UnitFactionSource.TryGet(0, in component, in arguments, out value);
            }
            case UnitSourceId.UnitManaBaseMaxMp:
            {
                if (!_UnitManaComponentLookup.TryGetComponent(entity, out UnitManaComponent component))
                    return false;
                return UnitManaSource.TryGet(0, in component, in arguments, out value);
            }
            case UnitSourceId.UnitManaBaseMaxMpOffset:
            {
                if (!_UnitManaComponentLookup.TryGetComponent(entity, out UnitManaComponent component))
                    return false;
                return UnitManaSource.TryGet(1, in component, in arguments, out value);
            }
            case UnitSourceId.UnitManaBaseMpRegenPerSecond:
            {
                if (!_UnitManaComponentLookup.TryGetComponent(entity, out UnitManaComponent component))
                    return false;
                return UnitManaSource.TryGet(3, in component, in arguments, out value);
            }
            case UnitSourceId.UnitManaBaseMpRegenPerSecondOffset:
            {
                if (!_UnitManaComponentLookup.TryGetComponent(entity, out UnitManaComponent component))
                    return false;
                return UnitManaSource.TryGet(4, in component, in arguments, out value);
            }
            case UnitSourceId.UnitManaCurrentMana:
            {
                if (!_UnitManaComponentLookup.TryGetComponent(entity, out UnitManaComponent component))
                    return false;
                return UnitManaSource.TryGet(2, in component, in arguments, out value);
            }
            case UnitSourceId.UnitMoveBaseMaxAcceleration:
            {
                if (!_UnitMoveComponentLookup.TryGetComponent(entity, out UnitMoveComponent component))
                    return false;
                return UnitMoveSource.TryGet(1, in component, in arguments, out value);
            }
            case UnitSourceId.UnitMoveBaseMoveSpeed:
            {
                if (!_UnitMoveComponentLookup.TryGetComponent(entity, out UnitMoveComponent component))
                    return false;
                return UnitMoveSource.TryGet(0, in component, in arguments, out value);
            }
            case UnitSourceId.UnitMoveCommandSpeed:
            {
                if (!_UnitMoveComponentLookup.TryGetComponent(entity, out UnitMoveComponent component))
                    return false;
                return UnitMoveSource.TryGet(4, in component, in arguments, out value);
            }
            case UnitSourceId.UnitMoveDirection:
            {
                if (!_UnitMoveComponentLookup.TryGetComponent(entity, out UnitMoveComponent component))
                    return false;
                return UnitMoveSource.TryGet(2, in component, in arguments, out value);
            }
            case UnitSourceId.UnitMoveStateMoveMultiplier:
            {
                if (!_UnitMoveComponentLookup.TryGetComponent(entity, out UnitMoveComponent component))
                    return false;
                return UnitMoveSource.TryGet(3, in component, in arguments, out value);
            }
            case UnitSourceId.UnitTransformForward:
            {
                if (!_LocalTransformLookup.TryGetComponent(entity, out LocalTransform component))
                    return false;
                return UnitTransformSource.TryGet(1, in component, in arguments, out value);
            }
            case UnitSourceId.UnitTransformPosition:
            {
                if (!_LocalTransformLookup.TryGetComponent(entity, out LocalTransform component))
                    return false;
                return UnitTransformSource.TryGet(0, in component, in arguments, out value);
            }
            case UnitSourceId.UnitTransformScale:
            {
                if (!_LocalTransformLookup.TryGetComponent(entity, out LocalTransform component))
                    return false;
                return UnitTransformSource.TryGet(2, in component, in arguments, out value);
            }
            case UnitSourceId.UnitVitalityBaseDefense:
            {
                if (!_UnitVitalityComponentLookup.TryGetComponent(entity, out UnitVitalityComponent component))
                    return false;
                return UnitVitalitySource.TryGet(3, in component, in arguments, out value);
            }
            case UnitSourceId.UnitVitalityBaseHealthRegenPerSecond:
            {
                if (!_UnitVitalityComponentLookup.TryGetComponent(entity, out UnitVitalityComponent component))
                    return false;
                return UnitVitalitySource.TryGet(2, in component, in arguments, out value);
            }
            case UnitSourceId.UnitVitalityBaseMaxHealth:
            {
                if (!_UnitVitalityComponentLookup.TryGetComponent(entity, out UnitVitalityComponent component))
                    return false;
                return UnitVitalitySource.TryGet(0, in component, in arguments, out value);
            }
            case UnitSourceId.UnitVitalityCurrentHealth:
            {
                if (!_UnitVitalityComponentLookup.TryGetComponent(entity, out UnitVitalityComponent component))
                    return false;
                return UnitVitalitySource.TryGet(1, in component, in arguments, out value);
            }
            case UnitSourceId.UnitAttackChantDurationMultiplier:
                return UnitAttackSource.TryGetResolved(6, entity, in _UnitAttackComponentLookup, in _UnitModifierComponentLookup, in arguments, out value);
            case UnitSourceId.UnitAttackRealAttackPower:
                return UnitAttackSource.TryGetResolved(3, entity, in _UnitAttackComponentLookup, in _UnitModifierComponentLookup, in arguments, out value);
            case UnitSourceId.UnitAttackRealChantSpeedBonus:
                return UnitAttackSource.TryGetResolved(5, entity, in _UnitAttackComponentLookup, in _UnitModifierComponentLookup, in arguments, out value);
            case UnitSourceId.UnitAttackRealSkillRange:
                return UnitAttackSource.TryGetResolved(4, entity, in _UnitAttackComponentLookup, in _UnitModifierComponentLookup, in arguments, out value);
            case UnitSourceId.UnitBuffsCount:
                return UnitBuffSource.TryGet(0, entity, in _UnitBuffComponentLookup, in _UnitBuffElementBufferLookup, in arguments, out value);
            case UnitSourceId.UnitBuffsFindIndex:
                return UnitBuffSource.TryGet(7, entity, in _UnitBuffComponentLookup, in _UnitBuffElementBufferLookup, in arguments, out value);
            case UnitSourceId.UnitBuffsHas:
                return UnitBuffSource.TryGet(8, entity, in _UnitBuffComponentLookup, in _UnitBuffElementBufferLookup, in arguments, out value);
            case UnitSourceId.UnitBuffsHasOriginAt:
                return UnitBuffSource.TryGet(4, entity, in _UnitBuffComponentLookup, in _UnitBuffElementBufferLookup, in arguments, out value);
            case UnitSourceId.UnitBuffsIdAt:
                return UnitBuffSource.TryGet(1, entity, in _UnitBuffComponentLookup, in _UnitBuffElementBufferLookup, in arguments, out value);
            case UnitSourceId.UnitBuffsOriginEntityAt:
                return UnitBuffSource.TryGet(5, entity, in _UnitBuffComponentLookup, in _UnitBuffElementBufferLookup, in arguments, out value);
            case UnitSourceId.UnitBuffsRemainingTimeAt:
                return UnitBuffSource.TryGet(2, entity, in _UnitBuffComponentLookup, in _UnitBuffElementBufferLookup, in arguments, out value);
            case UnitSourceId.UnitBuffsSourceSkillIdAt:
                return UnitBuffSource.TryGet(6, entity, in _UnitBuffComponentLookup, in _UnitBuffElementBufferLookup, in arguments, out value);
            case UnitSourceId.UnitBuffsStackCount:
                return UnitBuffSource.TryGet(9, entity, in _UnitBuffComponentLookup, in _UnitBuffElementBufferLookup, in arguments, out value);
            case UnitSourceId.UnitBuffsStackCountAt:
                return UnitBuffSource.TryGet(3, entity, in _UnitBuffComponentLookup, in _UnitBuffElementBufferLookup, in arguments, out value);
            case UnitSourceId.UnitElementFirePower:
                return UnitElementSource.TryGet(1, entity, in _UnitElementComponentLookup, in _UnitModifierComponentLookup, in arguments, out value);
            case UnitSourceId.UnitElementLightningPower:
                return UnitElementSource.TryGet(2, entity, in _UnitElementComponentLookup, in _UnitModifierComponentLookup, in arguments, out value);
            case UnitSourceId.UnitElementWaterPower:
                return UnitElementSource.TryGet(0, entity, in _UnitElementComponentLookup, in _UnitModifierComponentLookup, in arguments, out value);
            case UnitSourceId.UnitElementWindPower:
                return UnitElementSource.TryGet(3, entity, in _UnitElementComponentLookup, in _UnitModifierComponentLookup, in arguments, out value);
            case UnitSourceId.UnitManaCurrentManaPercentage:
                return UnitManaSource.TryGetResolved(6, entity, in _UnitManaComponentLookup, in _UnitModifierComponentLookup, in arguments, out value);
            case UnitSourceId.UnitManaRealMaxMp:
                return UnitManaSource.TryGetResolved(5, entity, in _UnitManaComponentLookup, in _UnitModifierComponentLookup, in arguments, out value);
            case UnitSourceId.UnitManaRealMpRegenPerSecond:
                return UnitManaSource.TryGetResolved(7, entity, in _UnitManaComponentLookup, in _UnitModifierComponentLookup, in arguments, out value);
            case UnitSourceId.UnitMoveRealMaxAcceleration:
                return UnitMoveSource.TryGetResolved(6, entity, in _UnitMoveComponentLookup, in _UnitModifierComponentLookup, in arguments, out value);
            case UnitSourceId.UnitMoveRealMoveSpeed:
                return UnitMoveSource.TryGetResolved(5, entity, in _UnitMoveComponentLookup, in _UnitModifierComponentLookup, in arguments, out value);
            case UnitSourceId.UnitSelfEntity:
                return UnitSkillReleaseSource.TryGet(0, entity, in _UnitSkillReleaseComponentLookup, in arguments, out value);
            case UnitSourceId.UnitVitalityCurrentHealthPercentage:
                return UnitVitalitySource.TryGetResolved(5, entity, in _UnitVitalityComponentLookup, in _UnitModifierComponentLookup, in arguments, out value);
            case UnitSourceId.UnitVitalityRealDefense:
                return UnitVitalitySource.TryGetResolved(7, entity, in _UnitVitalityComponentLookup, in _UnitModifierComponentLookup, in arguments, out value);
            case UnitSourceId.UnitVitalityRealHealthRegenPerSecond:
                return UnitVitalitySource.TryGetResolved(6, entity, in _UnitVitalityComponentLookup, in _UnitModifierComponentLookup, in arguments, out value);
            case UnitSourceId.UnitVitalityRealMaxHealth:
                return UnitVitalitySource.TryGetResolved(4, entity, in _UnitVitalityComponentLookup, in _UnitModifierComponentLookup, in arguments, out value);
            case UnitSourceId.UnitDeathIsActive:
                return UnitDeathSource.TryGet(0, entity, in _UnitDeathComponentLookup, in arguments, out value);
            case UnitSourceId.UnitFactionIsEnemyTo:
                return UnitFactionSource.TryGetRelation(1, entity, in _UnitFactionComponentLookup, in arguments, out value);
            case UnitSourceId.UnitTransformDirectionTo:
                return UnitTransformSource.TryGetRelation(4, entity, in _LocalTransformLookup, in arguments, out value);
            case UnitSourceId.UnitTransformPositionOf:
                return UnitTransformSource.TryGetRelation(3, entity, in _LocalTransformLookup, in arguments, out value);
            case UnitSourceId.UnitPerceptionNearestUnitByFaction:
                return UnitPerceptionSource.TryGet(6, entity, in _UnitPerceptionComponentLookup, in _UnitPerceptionUnitElementBufferLookup, in _UnitFactionComponentLookup, in _UnitDeathComponentLookup, in _DestroyEntityFlagLookup, in arguments, out value);
            case UnitSourceId.UnitPerceptionNearestUnitByName:
                return UnitPerceptionSource.TryGet(9, entity, in _UnitPerceptionComponentLookup, in _UnitPerceptionUnitElementBufferLookup, in _UnitFactionComponentLookup, in _UnitDeathComponentLookup, in _DestroyEntityFlagLookup, in arguments, out value);
            case UnitSourceId.UnitPerceptionSearchRadius:
                return UnitPerceptionSource.TryGet(0, entity, in _UnitPerceptionComponentLookup, in _UnitPerceptionUnitElementBufferLookup, in _UnitFactionComponentLookup, in _UnitDeathComponentLookup, in _DestroyEntityFlagLookup, in arguments, out value);
            case UnitSourceId.UnitPerceptionUnitAt:
                return UnitPerceptionSource.TryGet(3, entity, in _UnitPerceptionComponentLookup, in _UnitPerceptionUnitElementBufferLookup, in _UnitFactionComponentLookup, in _UnitDeathComponentLookup, in _DestroyEntityFlagLookup, in arguments, out value);
            case UnitSourceId.UnitPerceptionUnitByFactionAt:
                return UnitPerceptionSource.TryGet(5, entity, in _UnitPerceptionComponentLookup, in _UnitPerceptionUnitElementBufferLookup, in _UnitFactionComponentLookup, in _UnitDeathComponentLookup, in _DestroyEntityFlagLookup, in arguments, out value);
            case UnitSourceId.UnitPerceptionUnitByNameAt:
                return UnitPerceptionSource.TryGet(8, entity, in _UnitPerceptionComponentLookup, in _UnitPerceptionUnitElementBufferLookup, in _UnitFactionComponentLookup, in _UnitDeathComponentLookup, in _DestroyEntityFlagLookup, in arguments, out value);
            case UnitSourceId.UnitPerceptionUnitCount:
                return UnitPerceptionSource.TryGet(2, entity, in _UnitPerceptionComponentLookup, in _UnitPerceptionUnitElementBufferLookup, in _UnitFactionComponentLookup, in _UnitDeathComponentLookup, in _DestroyEntityFlagLookup, in arguments, out value);
            case UnitSourceId.UnitPerceptionUnitName:
                return UnitPerceptionSource.TryGet(1, entity, in _UnitPerceptionComponentLookup, in _UnitPerceptionUnitElementBufferLookup, in _UnitFactionComponentLookup, in _UnitDeathComponentLookup, in _DestroyEntityFlagLookup, in arguments, out value);
            case UnitSourceId.UnitPerceptionUnitsByFactionCount:
                return UnitPerceptionSource.TryGet(4, entity, in _UnitPerceptionComponentLookup, in _UnitPerceptionUnitElementBufferLookup, in _UnitFactionComponentLookup, in _UnitDeathComponentLookup, in _DestroyEntityFlagLookup, in arguments, out value);
            case UnitSourceId.UnitPerceptionUnitsByNameCount:
                return UnitPerceptionSource.TryGet(7, entity, in _UnitPerceptionComponentLookup, in _UnitPerceptionUnitElementBufferLookup, in _UnitFactionComponentLookup, in _UnitDeathComponentLookup, in _DestroyEntityFlagLookup, in arguments, out value);
            case UnitSourceId.UnitVariablesConsumerCount:
                return UnitVariableSource.TryGet(1, entity, in _UnitVariableComponentLookup, in _UnitVariableElementBufferLookup, in _UnitVariableConsumerElementBufferLookup, in _DestroyEntityFlagLookup, in arguments, out value);
            case UnitSourceId.UnitVariablesCount:
                return UnitVariableSource.TryGet(0, entity, in _UnitVariableComponentLookup, in _UnitVariableElementBufferLookup, in _UnitVariableConsumerElementBufferLookup, in _DestroyEntityFlagLookup, in arguments, out value);
            case UnitSourceId.UnitVariablesGet:
                return UnitVariableSource.TryGet(4, entity, in _UnitVariableComponentLookup, in _UnitVariableElementBufferLookup, in _UnitVariableConsumerElementBufferLookup, in _DestroyEntityFlagLookup, in arguments, out value);
            case UnitSourceId.UnitVariablesGetBool:
                return UnitVariableSource.TryGet(6, entity, in _UnitVariableComponentLookup, in _UnitVariableElementBufferLookup, in _UnitVariableConsumerElementBufferLookup, in _DestroyEntityFlagLookup, in arguments, out value);
            case UnitSourceId.UnitVariablesGetEntity:
                return UnitVariableSource.TryGet(9, entity, in _UnitVariableComponentLookup, in _UnitVariableElementBufferLookup, in _UnitVariableConsumerElementBufferLookup, in _DestroyEntityFlagLookup, in arguments, out value);
            case UnitSourceId.UnitVariablesGetFloat2:
                return UnitVariableSource.TryGet(7, entity, in _UnitVariableComponentLookup, in _UnitVariableElementBufferLookup, in _UnitVariableConsumerElementBufferLookup, in _DestroyEntityFlagLookup, in arguments, out value);
            case UnitSourceId.UnitVariablesGetFloat3:
                return UnitVariableSource.TryGet(8, entity, in _UnitVariableComponentLookup, in _UnitVariableElementBufferLookup, in _UnitVariableConsumerElementBufferLookup, in _DestroyEntityFlagLookup, in arguments, out value);
            case UnitSourceId.UnitVariablesGetNumber:
                return UnitVariableSource.TryGet(5, entity, in _UnitVariableComponentLookup, in _UnitVariableElementBufferLookup, in _UnitVariableConsumerElementBufferLookup, in _DestroyEntityFlagLookup, in arguments, out value);
            case UnitSourceId.UnitVariablesGetString:
                return UnitVariableSource.TryGet(10, entity, in _UnitVariableComponentLookup, in _UnitVariableElementBufferLookup, in _UnitVariableConsumerElementBufferLookup, in _DestroyEntityFlagLookup, in arguments, out value);
            case UnitSourceId.UnitVariablesHas:
                return UnitVariableSource.TryGet(3, entity, in _UnitVariableComponentLookup, in _UnitVariableElementBufferLookup, in _UnitVariableConsumerElementBufferLookup, in _DestroyEntityFlagLookup, in arguments, out value);
            case UnitSourceId.UnitVariablesOther:
                return UnitVariableSource.TryGet(2, entity, in _UnitVariableComponentLookup, in _UnitVariableElementBufferLookup, in _UnitVariableConsumerElementBufferLookup, in _DestroyEntityFlagLookup, in arguments, out value);
            case UnitSourceId.WorldVariablesCount:
                return WorldVariableSource.TryGet(0, _globalEntity, in _WorldVariableComponentLookup, in _WorldVariableElementBufferLookup, in arguments, out value);
            case UnitSourceId.WorldVariablesGet:
                return WorldVariableSource.TryGet(2, _globalEntity, in _WorldVariableComponentLookup, in _WorldVariableElementBufferLookup, in arguments, out value);
            case UnitSourceId.WorldVariablesGetBool:
                return WorldVariableSource.TryGet(4, _globalEntity, in _WorldVariableComponentLookup, in _WorldVariableElementBufferLookup, in arguments, out value);
            case UnitSourceId.WorldVariablesGetEntity:
                return WorldVariableSource.TryGet(7, _globalEntity, in _WorldVariableComponentLookup, in _WorldVariableElementBufferLookup, in arguments, out value);
            case UnitSourceId.WorldVariablesGetFloat2:
                return WorldVariableSource.TryGet(5, _globalEntity, in _WorldVariableComponentLookup, in _WorldVariableElementBufferLookup, in arguments, out value);
            case UnitSourceId.WorldVariablesGetFloat3:
                return WorldVariableSource.TryGet(6, _globalEntity, in _WorldVariableComponentLookup, in _WorldVariableElementBufferLookup, in arguments, out value);
            case UnitSourceId.WorldVariablesGetNumber:
                return WorldVariableSource.TryGet(3, _globalEntity, in _WorldVariableComponentLookup, in _WorldVariableElementBufferLookup, in arguments, out value);
            case UnitSourceId.WorldVariablesGetString:
                return WorldVariableSource.TryGet(8, _globalEntity, in _WorldVariableComponentLookup, in _WorldVariableElementBufferLookup, in arguments, out value);
            case UnitSourceId.WorldVariablesHas:
                return WorldVariableSource.TryGet(1, _globalEntity, in _WorldVariableComponentLookup, in _WorldVariableElementBufferLookup, in arguments, out value);
            case UnitSourceId.WorldInteractionIsInteracting:
            {
                if (!_InteractionCandidateComponentLookup.TryGetComponent(_globalEntity, out InteractionCandidateComponent component))
                    return false;
                return GameInteractionSource.TryGet(3, in component, in arguments, out value);
            }
            case UnitSourceId.PlayerSkillCurrentAdditionId:
                return PlayerCurrentSkillSource.TryGetDerived(4, new UnitSourceAccessContext(entity, _globalEntity), in _PlayerCurrentSkillComponentLookup, in _PlayerSkillChainElementBufferLookup, in _PlayerSkillChainSlotElementBufferLookup, in _PlayerSkillDefinitionRegistryComponentLookup, in arguments, out value);
            case UnitSourceId.PlayerSkillCurrentInputType:
                return PlayerCurrentSkillSource.TryGetDerived(3, new UnitSourceAccessContext(entity, _globalEntity), in _PlayerCurrentSkillComponentLookup, in _PlayerSkillChainElementBufferLookup, in _PlayerSkillChainSlotElementBufferLookup, in _PlayerSkillDefinitionRegistryComponentLookup, in arguments, out value);
            case UnitSourceId.PlayerSkillCurrentSkillId:
                return PlayerCurrentSkillSource.TryGetDerived(2, new UnitSourceAccessContext(entity, _globalEntity), in _PlayerCurrentSkillComponentLookup, in _PlayerSkillChainElementBufferLookup, in _PlayerSkillChainSlotElementBufferLookup, in _PlayerSkillDefinitionRegistryComponentLookup, in arguments, out value);
            case UnitSourceId.PlayerSkillGetChainLength:
                return PlayerSkillChainSource.TryGet(10, new UnitSourceAccessContext(entity, _globalEntity), in _PlayerInputComponentLookup, in _PlayerSkillChainElementBufferLookup, in _PlayerSkillChainSlotElementBufferLookup, in _PlayerSkillDefinitionRegistryComponentLookup, in arguments, out value);
            case UnitSourceId.PlayerSkillGetChainSkillAdditionId:
                return PlayerSkillChainSource.TryGet(12, new UnitSourceAccessContext(entity, _globalEntity), in _PlayerInputComponentLookup, in _PlayerSkillChainElementBufferLookup, in _PlayerSkillChainSlotElementBufferLookup, in _PlayerSkillDefinitionRegistryComponentLookup, in arguments, out value);
            case UnitSourceId.PlayerSkillGetChainSkillId:
                return PlayerSkillChainSource.TryGet(11, new UnitSourceAccessContext(entity, _globalEntity), in _PlayerInputComponentLookup, in _PlayerSkillChainElementBufferLookup, in _PlayerSkillChainSlotElementBufferLookup, in _PlayerSkillDefinitionRegistryComponentLookup, in arguments, out value);
            case UnitSourceId.PlayerSkillGetCurrentChainLength:
                return PlayerSkillChainSource.TryGet(2, new UnitSourceAccessContext(entity, _globalEntity), in _PlayerInputComponentLookup, in _PlayerSkillChainElementBufferLookup, in _PlayerSkillChainSlotElementBufferLookup, in _PlayerSkillDefinitionRegistryComponentLookup, in arguments, out value);
            case UnitSourceId.PlayerSkillGetCurrentSkillAdditionId:
                return PlayerSkillChainSource.TryGet(4, new UnitSourceAccessContext(entity, _globalEntity), in _PlayerInputComponentLookup, in _PlayerSkillChainElementBufferLookup, in _PlayerSkillChainSlotElementBufferLookup, in _PlayerSkillDefinitionRegistryComponentLookup, in arguments, out value);
            case UnitSourceId.PlayerSkillGetCurrentSkillChantDuration:
                return PlayerSkillChainSource.TryGet(7, new UnitSourceAccessContext(entity, _globalEntity), in _PlayerInputComponentLookup, in _PlayerSkillChainElementBufferLookup, in _PlayerSkillChainSlotElementBufferLookup, in _PlayerSkillDefinitionRegistryComponentLookup, in arguments, out value);
            case UnitSourceId.PlayerSkillGetCurrentSkillId:
                return PlayerSkillChainSource.TryGet(3, new UnitSourceAccessContext(entity, _globalEntity), in _PlayerInputComponentLookup, in _PlayerSkillChainElementBufferLookup, in _PlayerSkillChainSlotElementBufferLookup, in _PlayerSkillDefinitionRegistryComponentLookup, in arguments, out value);
            case UnitSourceId.PlayerSkillGetCurrentSkillMpCost:
                return PlayerSkillChainSource.TryGet(6, new UnitSourceAccessContext(entity, _globalEntity), in _PlayerInputComponentLookup, in _PlayerSkillChainElementBufferLookup, in _PlayerSkillChainSlotElementBufferLookup, in _PlayerSkillDefinitionRegistryComponentLookup, in arguments, out value);
            case UnitSourceId.PlayerSkillGetCurrentSkillRuntimeType:
                return PlayerSkillChainSource.TryGet(8, new UnitSourceAccessContext(entity, _globalEntity), in _PlayerInputComponentLookup, in _PlayerSkillChainElementBufferLookup, in _PlayerSkillChainSlotElementBufferLookup, in _PlayerSkillDefinitionRegistryComponentLookup, in arguments, out value);
            case UnitSourceId.PlayerSkillGetInputType:
                return PlayerSkillChainSource.TryGet(18, new UnitSourceAccessContext(entity, _globalEntity), in _PlayerInputComponentLookup, in _PlayerSkillChainElementBufferLookup, in _PlayerSkillChainSlotElementBufferLookup, in _PlayerSkillDefinitionRegistryComponentLookup, in arguments, out value);
            case UnitSourceId.PlayerSkillGetSkillCastingMoveMultiplier:
                return PlayerSkillChainSource.TryGet(16, new UnitSourceAccessContext(entity, _globalEntity), in _PlayerInputComponentLookup, in _PlayerSkillChainElementBufferLookup, in _PlayerSkillChainSlotElementBufferLookup, in _PlayerSkillDefinitionRegistryComponentLookup, in arguments, out value);
            case UnitSourceId.PlayerSkillGetSkillChantDuration:
                return PlayerSkillChainSource.TryGet(15, new UnitSourceAccessContext(entity, _globalEntity), in _PlayerInputComponentLookup, in _PlayerSkillChainElementBufferLookup, in _PlayerSkillChainSlotElementBufferLookup, in _PlayerSkillDefinitionRegistryComponentLookup, in arguments, out value);
            case UnitSourceId.PlayerSkillGetSkillMpCost:
                return PlayerSkillChainSource.TryGet(14, new UnitSourceAccessContext(entity, _globalEntity), in _PlayerInputComponentLookup, in _PlayerSkillChainElementBufferLookup, in _PlayerSkillChainSlotElementBufferLookup, in _PlayerSkillDefinitionRegistryComponentLookup, in arguments, out value);
            case UnitSourceId.PlayerSkillGetSkillRuntimeType:
                return PlayerSkillChainSource.TryGet(17, new UnitSourceAccessContext(entity, _globalEntity), in _PlayerInputComponentLookup, in _PlayerSkillChainElementBufferLookup, in _PlayerSkillChainSlotElementBufferLookup, in _PlayerSkillDefinitionRegistryComponentLookup, in arguments, out value);
            case UnitSourceId.PlayerSkillHasCurrentSkill:
                return PlayerCurrentSkillSource.TryGetDerived(5, new UnitSourceAccessContext(entity, _globalEntity), in _PlayerCurrentSkillComponentLookup, in _PlayerSkillChainElementBufferLookup, in _PlayerSkillChainSlotElementBufferLookup, in _PlayerSkillDefinitionRegistryComponentLookup, in arguments, out value);
            case UnitSourceId.PlayerSkillHasCurrentSkillAt:
                return PlayerSkillChainSource.TryGet(5, new UnitSourceAccessContext(entity, _globalEntity), in _PlayerInputComponentLookup, in _PlayerSkillChainElementBufferLookup, in _PlayerSkillChainSlotElementBufferLookup, in _PlayerSkillDefinitionRegistryComponentLookup, in arguments, out value);
            case UnitSourceId.PlayerSkillHasSkill:
                return PlayerSkillChainSource.TryGet(13, new UnitSourceAccessContext(entity, _globalEntity), in _PlayerInputComponentLookup, in _PlayerSkillChainElementBufferLookup, in _PlayerSkillChainSlotElementBufferLookup, in _PlayerSkillDefinitionRegistryComponentLookup, in arguments, out value);
            case UnitSourceId.PlayerSkillIsChainEmpty:
                return PlayerSkillChainSource.TryGet(9, new UnitSourceAccessContext(entity, _globalEntity), in _PlayerInputComponentLookup, in _PlayerSkillChainElementBufferLookup, in _PlayerSkillChainSlotElementBufferLookup, in _PlayerSkillDefinitionRegistryComponentLookup, in arguments, out value);
            case UnitSourceId.PlayerSkillIsCurrentChainEmpty:
                return PlayerSkillChainSource.TryGet(1, new UnitSourceAccessContext(entity, _globalEntity), in _PlayerInputComponentLookup, in _PlayerSkillChainElementBufferLookup, in _PlayerSkillChainSlotElementBufferLookup, in _PlayerSkillDefinitionRegistryComponentLookup, in arguments, out value);
            case UnitSourceId.UnitInterestPointArrivalDistance:
            case UnitSourceId.UnitInterestPointCurrentTarget:
            case UnitSourceId.UnitInterestPointEncounterId:
            case UnitSourceId.UnitInterestPointHasPatrol:
            case UnitSourceId.UnitInterestPointPatrolSpeed:
            case UnitSourceId.UnitInterestPointPlayerDistance:
            case UnitSourceId.UnitInterestPointShouldSpawnPatrol:
            case UnitSourceId.UnitInterestPointSpawnDistance:
            case UnitSourceId.UnitInterestPointSquadId:
            case UnitSourceId.UnitInterestPointTargetReached:
            {
                if (!_DungeonInterestPointComponentLookup.TryGetComponent(entity, out DungeonInterestPointComponent component))
                    return false;
                int operation = sourceId switch
                {
                    UnitSourceId.UnitInterestPointEncounterId => 0,
                    UnitSourceId.UnitInterestPointSquadId => 1,
                    UnitSourceId.UnitInterestPointSpawnDistance => 2,
                    UnitSourceId.UnitInterestPointPatrolSpeed => 3,
                    UnitSourceId.UnitInterestPointArrivalDistance => 4,
                    UnitSourceId.UnitInterestPointHasPatrol => 5,
                    UnitSourceId.UnitInterestPointCurrentTarget => 6,
                    UnitSourceId.UnitInterestPointPlayerDistance => 7,
                    UnitSourceId.UnitInterestPointShouldSpawnPatrol => 8,
                    UnitSourceId.UnitInterestPointTargetReached => 9,
                    _ => -1,
                };
                return DungeonInterestPointSource.TryGet(operation, in component, in arguments, out value);
            }
            default:
                return false;
        }
    }

    public bool TrySet(Entity entity, UnitSourceId sourceId, in UnitSourceArguments arguments)
    {
        switch (sourceId)
        {
            case UnitSourceId.PlayerSkillCurrentChainSlotClear:
            {
                if (!_PlayerCurrentSkillComponentLookup.HasComponent(entity))
                    return false;
                RefRW<PlayerCurrentSkillComponent> component = _PlayerCurrentSkillComponentLookup.GetRefRW(entity);
                return PlayerCurrentSkillSource.TryClear(1, ref component.ValueRW, in arguments);
            }
            case UnitSourceId.UnitAnimationPlay:
                return UnitAnimationSource.TrySet(1, new UnitSourceAccessContext(entity, _globalEntity), ref _UnitAnimationComponentLookup, in _WorldStateComponentLookup, in arguments);
            case UnitSourceId.UnitAnimationSetName:
                return UnitAnimationSource.TrySet(0, new UnitSourceAccessContext(entity, _globalEntity), ref _UnitAnimationComponentLookup, in _WorldStateComponentLookup, in arguments);
            case UnitSourceId.UnitBuffsRemove:
                return UnitBuffSource.TrySet(0, entity, ref _UnitBuffComponentLookup, ref _UnitBuffElementBufferLookup, in arguments);
            case UnitSourceId.UnitBuffsRemoveStacks:
                return UnitBuffSource.TrySet(1, entity, ref _UnitBuffComponentLookup, ref _UnitBuffElementBufferLookup, in arguments);
            case UnitSourceId.UnitFacingSetDirection:
            {
                if (!_UnitFacingComponentLookup.HasComponent(entity))
                    return false;
                RefRW<UnitFacingComponent> component = _UnitFacingComponentLookup.GetRefRW(entity);
                return UnitFacingSource.TrySet(0, ref component.ValueRW, in arguments);
            }
            case UnitSourceId.UnitManaCost:
            {
                if (!_UnitManaComponentLookup.HasComponent(entity))
                    return false;
                RefRW<UnitManaComponent> component = _UnitManaComponentLookup.GetRefRW(entity);
                return UnitManaSource.TrySet(0, ref component.ValueRW, in arguments);
            }
            case UnitSourceId.UnitMoveSetCommandSpeed:
            {
                if (!_UnitMoveComponentLookup.HasComponent(entity))
                    return false;
                RefRW<UnitMoveComponent> component = _UnitMoveComponentLookup.GetRefRW(entity);
                return UnitMoveSource.TrySet(4, ref component.ValueRW, in arguments);
            }
            case UnitSourceId.UnitMoveSetDirection:
            {
                if (!_UnitMoveComponentLookup.HasComponent(entity))
                    return false;
                RefRW<UnitMoveComponent> component = _UnitMoveComponentLookup.GetRefRW(entity);
                return UnitMoveSource.TrySet(0, ref component.ValueRW, in arguments);
            }
            case UnitSourceId.UnitMoveSetFrameVelocity:
            {
                if (!_UnitMoveComponentLookup.HasComponent(entity))
                    return false;
                RefRW<UnitMoveComponent> component = _UnitMoveComponentLookup.GetRefRW(entity);
                return UnitMoveSource.TrySet(2, ref component.ValueRW, in arguments);
            }
            case UnitSourceId.UnitMoveSetStateMoveMultiplier:
            {
                if (!_UnitMoveComponentLookup.HasComponent(entity))
                    return false;
                RefRW<UnitMoveComponent> component = _UnitMoveComponentLookup.GetRefRW(entity);
                return UnitMoveSource.TrySet(3, ref component.ValueRW, in arguments);
            }
            case UnitSourceId.UnitMoveSetVelocity:
            {
                if (!_UnitMoveComponentLookup.HasComponent(entity))
                    return false;
                RefRW<UnitMoveComponent> component = _UnitMoveComponentLookup.GetRefRW(entity);
                return UnitMoveSource.TrySet(1, ref component.ValueRW, in arguments);
            }
            case UnitSourceId.UnitNavigationSetDestination:
            {
                if (!_UnitNavigationComponentLookup.HasComponent(entity))
                    return false;
                RefRW<UnitNavigationComponent> component = _UnitNavigationComponentLookup.GetRefRW(entity);
                return UnitNavigationSource.TrySet(0, ref component.ValueRW, in arguments);
            }
            case UnitSourceId.UnitNavigationStop:
            {
                if (!_UnitNavigationComponentLookup.HasComponent(entity))
                    return false;
                RefRW<UnitNavigationComponent> component = _UnitNavigationComponentLookup.GetRefRW(entity);
                return UnitNavigationSource.TrySet(1, ref component.ValueRW, in arguments);
            }
            case UnitSourceId.UnitVariablesRemove:
                return UnitVariableSource.TrySet(1, entity, ref _UnitVariableComponentLookup, ref _UnitVariableElementBufferLookup, ref _UnitVariableConsumerElementBufferLookup, in arguments);
            case UnitSourceId.UnitVariablesSet:
                return UnitVariableSource.TrySet(0, entity, ref _UnitVariableComponentLookup, ref _UnitVariableElementBufferLookup, ref _UnitVariableConsumerElementBufferLookup, in arguments);
            case UnitSourceId.UnitVariablesSetOther:
                return UnitVariableSource.TrySet(2, entity, ref _UnitVariableComponentLookup, ref _UnitVariableElementBufferLookup, ref _UnitVariableConsumerElementBufferLookup, in arguments);
            case UnitSourceId.WorldVariablesRemove:
                return WorldVariableSource.TrySet(1, _globalEntity, in _WorldVariableComponentLookup, ref _WorldVariableElementBufferLookup, in arguments);
            case UnitSourceId.WorldVariablesSet:
                return WorldVariableSource.TrySet(0, _globalEntity, in _WorldVariableComponentLookup, ref _WorldVariableElementBufferLookup, in arguments);
            case UnitSourceId.PlayerSkillCurrentChainSlotSet:
                return PlayerCurrentSkillSource.TrySet(0, new UnitSourceAccessContext(entity, _globalEntity), ref _PlayerCurrentSkillComponentLookup, in _PlayerSkillChainElementBufferLookup, in _PlayerSkillChainSlotElementBufferLookup, in _PlayerSkillDefinitionRegistryComponentLookup, in arguments);
            case UnitSourceId.PlayerSkillPendingExtraModifiersAdd:
                return PlayerCurrentSkillSource.TrySet(2, new UnitSourceAccessContext(entity, _globalEntity), ref _PlayerCurrentSkillComponentLookup, in _PlayerSkillChainElementBufferLookup, in _PlayerSkillChainSlotElementBufferLookup, in _PlayerSkillDefinitionRegistryComponentLookup, in arguments);
            case UnitSourceId.UnitInterestPointSetNextPatrolTarget:
                return DungeonInterestPointSource.TrySet(1, entity, ref _DungeonInterestPointComponentLookup, ref _UnitVariableElementBufferLookup, in _DungeonInterestPointCandidateElementBufferLookup, in _UnitVariableConsumerElementBufferLookup, in _UnitVariableComponentLookup, in _LocalTransformLookup, in _DestroyEntityFlagLookup, in arguments);
            case UnitSourceId.UnitInterestPointSetPatrolActive:
                return DungeonInterestPointSource.TrySet(0, entity, ref _DungeonInterestPointComponentLookup, ref _UnitVariableElementBufferLookup, in _DungeonInterestPointCandidateElementBufferLookup, in _UnitVariableConsumerElementBufferLookup, in _UnitVariableComponentLookup, in _LocalTransformLookup, in _DestroyEntityFlagLookup, in arguments);
            case UnitSourceId.UnitInterestPointSetPatrolSpeed:
                return DungeonInterestPointSource.TrySet(2, entity, ref _DungeonInterestPointComponentLookup, ref _UnitVariableElementBufferLookup, in _DungeonInterestPointCandidateElementBufferLookup, in _UnitVariableConsumerElementBufferLookup, in _UnitVariableComponentLookup, in _LocalTransformLookup, in _DestroyEntityFlagLookup, in arguments);
            default:
                return false;
        }
    }

    public bool TryGetInteraction(out InteractionRequestSnapshot request)
    {
        request = default;
        if (!_InteractionCandidateComponentLookup.TryGetComponent(_globalEntity, out InteractionCandidateComponent candidate))
            return false;
        return GameInteractionSource.TryGetInteraction(in candidate, out request);
    }

}
