using System;
using UnityEngine;

namespace Mimicraft.MapKit
{
    /// <summary>
    /// Something that happened to a map entity, which its outputs react to: Hammer's outputs (plan §16).
    ///
    /// Part of the map format, like every name in this assembly (KitAssemblyAudit.Frozen): a published map
    /// stores these numbers. Never renumber one, never reuse one; add new ones with new numbers.
    /// </summary>
    public enum MapEvent
    {
        Pressed = 0,
        PressedWhileLocked = 1,

        Started = 10,
        ArrivedAtStop = 11,
        ArrivedAtEnd = 12,
        ArrivedAtStart = 13,
        Blocked = 14,

        Entered = 20,
        Exited = 21,
        Occupied = 22,
        Emptied = 23,

        Hurt = 30,
        Killed = 31,

        Finished = 40,

        Triggered = 50,

        Elapsed = 60,

        ReachedTarget = 70,

        RoundStarted = 80,
        HuntStarted = 81,
        RoundEnded = 82,

        Teleported = 90,
    }

    /// <summary>
    /// What an output does to its target: Hammer's inputs. The complete list of what a map can make happen;
    /// there is no way to name anything else (no method names, no UnityEvent, plan §16.1). Part of the map
    /// format: never renumber.
    /// </summary>
    public enum MapInput
    {
        Enable = 0,
        Disable = 1,
        Toggle = 2,

        Press = 10,
        Lock = 11,
        Unlock = 12,

        Forward = 20,
        Back = 21,
        GoToStop = 22,
        Reverse = 23,
        Pause = 24,
        Resume = 25,

        Play = 30,
        Stop = 31,

        Trigger = 40,

        Add = 50,
        Subtract = 51,
        SetValue = 52,
        ResetCount = 53,

        /// <summary>The player who set the chain off (pressed the button, walked into the trigger) takes
        /// this hurt zone's damage once: Hammer's !activator.</summary>
        HurtActivator = 60,

        /// <summary>The player who set the chain off goes to this teleport's destination.</summary>
        TeleportActivator = 61,
    }

    /// <summary>Who an entity reacts to. Only modes with Hunters and Modelers tell the two apart; in the
    /// others (Deathmatch, Practice, Gartic) everybody counts as both.</summary>
    public enum MapWho
    {
        Everyone = 0,
        Hunters = 1,
        Hiders = 2,
    }

    /// <summary>
    /// One connection: when this entity raises <see cref="onEvent"/>, <see cref="target"/> receives
    /// <see cref="input"/>, <see cref="delay"/> seconds later, with <see cref="parameter"/> where the input
    /// takes a number (a stop, an amount). <see cref="once"/>: only the first time in a round.
    ///
    /// A direct reference rather than Hammer's target names, so renaming an object never breaks a
    /// connection. The game takes nothing here on trust: a target from another scene, a NaN delay or an
    /// input the target does not take is ignored where it is read.
    /// </summary>
    [Serializable]
    public sealed class MapOutput
    {
        public MapEvent onEvent;
        public MapEntity target;
        public MapInput input;
        [Min(0f)] public float delay;
        public float parameter;
        public bool once;
    }
}
