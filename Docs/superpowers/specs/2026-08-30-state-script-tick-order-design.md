# State Script Tick Order Design

## Goal

Expose a per-node update priority for State Script nodes that update every frame, so graph authors can intentionally control monitor and timer observation order.

## Scope

- Add an integer `TickOrder` to `StateStateScriptNodeData`; missing values in existing JSON deserialize as `0`.
- Update the runtime state-node update list to sort by ascending `TickOrder`, preserving graph traversal order when values are equal.
- Show and edit `Tick Order` in the State Script node inspector for every state node.
- Configure the SkillChain `casting` monitor before the left-button monitor by assigning it a lower order.

## Non-Goals

- Do not change synchronous port-pulse ordering for Compare, SetValue, or action nodes.
- Do not add graph-level execution phases, delays, or automatic loop nodes.
- Do not change default ordering for existing nodes whose `TickOrder` remains `0`.

## Runtime Contract

State nodes are first discovered in the existing traversal order. The runtime then updates lower `TickOrder` values first. Equal values retain their original traversal order, making old graphs behaviorally compatible and making manual overrides local and explicit.

## SkillChain Configuration

The `casting` monitor is assigned `TickOrder = -10`; the left-button monitor remains `0`. After a chain completion writes `casting = false`, the casting monitor samples that false value before the held-button monitor can write true again. Its next observed false-to-true transition reliably starts the next held-cast iteration.
