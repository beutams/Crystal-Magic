using CrystalMagic.Core;
using Unity.Entities;

// 每次输入保留独立记录及当时的鼠标/技能链，不把一帧内多次操作压成 bool。
[InternalBufferCapacity(4)]
public struct PlayerInputEventElement : IBufferElementData
{
    public PlayerInputOperationType Type;
    public PlayerInputComponent Input;
}

public static class PlayerInputEventUtility
{
    public static void Append(
        EntityManager manager,
        Entity player,
        PlayerInputOperationType type,
        PlayerInputComponent input)
    {
        input.IsPrimaryHeld = type == PlayerInputOperationType.PrimaryPressed ? (byte)1 : (byte)0;
        input.IsInteractHeld = type == PlayerInputOperationType.Interact ? (byte)1 : (byte)0;
        input.IsSkillHeld = type == PlayerInputOperationType.SelectSkillChain ? (byte)1 : (byte)0;
        input.IsUsePropHeld = type == PlayerInputOperationType.UseProp ? (byte)1 : (byte)0;
        DynamicBuffer<PlayerInputEventElement> events = manager.HasBuffer<PlayerInputEventElement>(player)
            ? manager.GetBuffer<PlayerInputEventElement>(player)
            : manager.AddBuffer<PlayerInputEventElement>(player);
        events.Add(new PlayerInputEventElement { Type = type, Input = input });
    }
}
