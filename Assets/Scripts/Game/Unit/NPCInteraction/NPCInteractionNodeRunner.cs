public abstract class NPCInteractionNodeRunner
{
    public abstract void Enter(NPCInteractionSession session);
    public virtual void Update(NPCInteractionSession session, float deltaTime) { }
    public abstract bool IsCompleted(NPCInteractionSession session);
    public virtual void Exit(NPCInteractionSession session) { }
    public virtual void Cancel(NPCInteractionSession session) { }
}
