using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using BenchmarkDotNet.Running;
using CommunityToolkit.Diagnostics;

namespace Amphipod;

internal sealed partial class Amphipod {

    /// <summary>
    /// Represents a <see cref="State"/> visited while organizing the amphipod situation.
    /// </summary>
    private sealed partial class State : IEquatable<State> {

        /// <summary>Represents an enumeration of all possible amphipod types.</summary>
        private enum Amphipod { None, Amber, Bronze, Copper, Desert }

        /// <summary>Number of slots in the hallway of the burrow.</summary>
        private const int HallwaySlots = 11;

        /// <summary>Number of rooms for amphipods in the burrow.</summary>
        private const int Rooms = 4;

        /// <summary>
        /// Index at which the first amphipod symbol is expected within each line while parsing the
        /// initial situation using <see cref="Parse(IEnumerable{string})"/>.
        /// </summary>
        private const int IndexOfFirstSymbol = 3;

        /// <summary>
        /// Distance between rooms in the hallway (rooms are situated every other slot).
        /// </summary>
        private const int DistanceBetweenRooms = 2;

        /// <summary>
        /// Dictionary of symbols to amphipod types used while parsing the initial situation
        /// using <see cref="Parse(IEnumerable{string})"/>.
        /// </summary>
        private static readonly FrozenDictionary<char, Amphipod> SymbolToAmphipod =
            FrozenDictionary.ToFrozenDictionary([
                KeyValuePair.Create('.', Amphipod.None),
                KeyValuePair.Create('A', Amphipod.Amber),
                KeyValuePair.Create('B', Amphipod.Bronze),
                KeyValuePair.Create('C', Amphipod.Copper),
                KeyValuePair.Create('D', Amphipod.Desert)
            ]);

        /// <summary>
        /// Dictionary of amphipod types to symbols used for converting a <see cref="State"/>
        /// to its string representation using <see cref="ToString"/>.
        /// </summary>
        private static readonly FrozenDictionary<Amphipod, char> AmphipodToSymbol =
            FrozenDictionary.ToFrozenDictionary([
                KeyValuePair.Create(Amphipod.None, '.'),
                KeyValuePair.Create(Amphipod.Amber, 'A'),
                KeyValuePair.Create(Amphipod.Bronze, 'B'),
                KeyValuePair.Create(Amphipod.Copper, 'C'),
                KeyValuePair.Create(Amphipod.Desert, 'D')
            ]);

        /// <summary>Dictionary of amphipod types to their respective target rooms.</summary>
        private static readonly FrozenDictionary<Amphipod, int> AmphipodToTargetRoom =
            FrozenDictionary.ToFrozenDictionary([
                KeyValuePair.Create(Amphipod.Amber, 0),
                KeyValuePair.Create(Amphipod.Bronze, 1),
                KeyValuePair.Create(Amphipod.Copper, 2),
                KeyValuePair.Create(Amphipod.Desert, 3)
            ]);

        /// <summary>Dictionary of amphipod types to their respective energy factors.</summary>
        private static readonly FrozenDictionary<Amphipod, int> AmphipodToEnergyFactor =
            FrozenDictionary.ToFrozenDictionary([
                KeyValuePair.Create(Amphipod.Amber, 1),
                KeyValuePair.Create(Amphipod.Bronze, 10),
                KeyValuePair.Create(Amphipod.Copper, 100),
                KeyValuePair.Create(Amphipod.Desert, 1000)
            ]);

        /// <summary>Target slots in the hallway in which amphipods may stop.</summary>
        private static readonly FrozenSet<int> HallwayTargetSlots =
            FrozenSet.ToFrozenSet([0, 1, 3, 5, 7, 9, 10]);

        /// <summary>
        /// Hallway and room slots of this <see cref="State"/> containing the amphipods.
        /// </summary>
        private readonly ImmutableArray<Amphipod> slots;

        /// <summary>Depth of each room of this <see cref="State"/>.</summary>
        private readonly int roomDepth;

        /// <summary>Initializes a new <see cref="State"/> with a given sequence of slots.</summary>
        /// <param name="slots">Sequence of slots for the initialization.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="slots"/> has less than <see cref="HallwaySlots"/> slots,
        /// which is the minimum number of slots required without any rooms. Thrown as well if
        /// the number of remaining slots (after subtracting <see cref="HallwaySlots"/>) cannot
        /// be distributed evenly across the <see cref="Rooms"/> rooms.
        /// </exception>
        private State(ReadOnlySpan<Amphipod> slots) {
            Guard.IsGreaterThanOrEqualTo(slots.Length, HallwaySlots);
            int remainingSlots = slots.Length - HallwaySlots;
            if ((remainingSlots % Rooms) != 0) {
                throw new ArgumentOutOfRangeException(
                    nameof(slots),
                    $"Number of remaining slots ({remainingSlots}) is not evenly divisible by "
                        + $"the number of rooms ({Rooms})."
                );
            }
            this.slots = slots.ToImmutableArray();
            roomDepth = remainingSlots / Rooms;
        }

        [GeneratedRegex("^(?:##|  )#(?:[A-D]#){4}(?:##)?$")]
        private static partial Regex AmphipodLineRegex();

        /// <summary>Parses a <see cref="State"/> from a given sequence of lines.</summary>
        /// <remarks>
        /// The <paramref name="lines"/> must form a grid of the following form:
        /// <code>
        /// #############
        /// #...........#
        /// ###B#C#B#D###
        ///   #A#D#C#A#
        ///   #########
        /// </code>
        /// More specifically, it must fulfill the following requirements to construct a valid,
        /// organizable <see cref="State"/>:
        /// <list type="bullet">
        ///     <item>
        ///     It must have one or more lines of amphipod rooms, which match the
        ///     <see cref="AmphipodLineRegex"/>. Note that leading whitespace is not optional,
        ///     while any while trailing whitespace is explicitly not allowed.
        ///     </item>
        ///     <item>
        ///     There are always exactly <see cref="Rooms"/>, but they may have a different
        ///     depth than shown in the example grid. The depth must be the same across all rooms.
        ///     </item>
        ///     <item>
        ///     There must be exactly as many amphipods (indicated by symbols 'A' to 'D') as the
        ///     depth of each room, e. g. for rooms of depth two as in the example, there must be
        ///     exactly two amphipods of each type. The constructed <see cref="State"/> is not
        ///     organizable otherwise.
        ///     </item>
        ///     <item>
        ///     The first line ("#############"), hallway ("#...........#") and last line
        ///     ("  #########") are more or less ignored while parsing, but should be present anyway
        ///     to form a regular grid.
        ///     </item>
        /// </list>
        /// </remarks>
        /// <param name="lines">Sequence of lines to parse a <see cref="State"/> from.</param>
        /// <returns>A <see cref="State"/> parsed from the given sequence of lines.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="lines"/> is <see langword="null"/>.
        /// </exception>
        public static State Parse(IEnumerable<string> lines) {
            Guard.IsNotNull(lines);
            ReadOnlySpan<string> amphipodLines = [
                .. lines.Where(line => AmphipodLineRegex().IsMatch(line))
            ];
            int roomDepth = amphipodLines.Length;
            int totalSlots = HallwaySlots + (Rooms * roomDepth);
            Span<Amphipod> slots = stackalloc Amphipod[totalSlots];
            for (int room = 0; room < Rooms; room++) {
                int index = IndexOfFirstSymbol + (room * DistanceBetweenRooms);
                for (int depth = 0; depth < roomDepth; depth++) {
                    char symbol = amphipodLines[depth][index];
                    slots[HallwaySlots + (room * roomDepth) + depth] = SymbolToAmphipod[symbol];
                }
            }
            return new State(slots);
        }

        /// <summary>Returns the index of the slot in the hallway above a given room.</summary>
        /// <param name="room">
        /// Room to get the hallway slot above of (in the range [0; <see cref="Rooms"/> - 1]).
        /// </param>
        /// <returns>The index of the slot in the hallway above the given room.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int HallwaySlotAboveRoom(int room) => (room + 1) * DistanceBetweenRooms;

        /// <summary>Returns the index of the first slot of a specified room.</summary>
        /// <param name="room">
        /// Room to get the index of the first slot of (in the range [0; <see cref="Rooms"/> - 1]).
        /// </param>
        /// <returns>The index of the first slot of the specified room.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int FirstRoomSlot(int room) => HallwaySlots + (room * roomDepth);

        /// <summary>
        /// Determines if the hallway is blocked by another <see cref="Amphipod"/> when trying
        /// to move between two hallway slots.
        /// </summary>
        /// <remarks>
        /// The source and target hallway slots may be specified in any order, i. e. it is not
        /// required that <paramref name="sourceSlot"/> be less than or equal to
        /// <paramref name="targetSlot"/>.
        /// </remarks>
        /// <param name="sourceSlot">
        /// Source hallway slot to start at (in the range [0; <see cref="HallwaySlots"/> - 1]).
        /// </param>
        /// <param name="targetSlot">
        /// Target hallway slot to stop at (in the range [0; <see cref="HallwaySlots"/> - 1]).
        /// </param>
        /// <returns>
        /// <see langword="True"/> if the hallway is blocked by another <see cref="Amphipod"/> when
        /// trying to move from <paramref name="sourceSlot"/> to
        /// <paramref name="targetSlot"/>, otherwise <see langword="false"/>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsHallwayBlocked(int sourceSlot, int targetSlot) {
            if (sourceSlot > targetSlot) {
                (sourceSlot, targetSlot) = (targetSlot, sourceSlot);
            }
            foreach (Amphipod other in slots.AsSpan(sourceSlot + 1, targetSlot - sourceSlot - 1)) {
                if (other != Amphipod.None) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Tries to enter the target room of a certain <see cref="Amphipod"/> at a given hallway
        /// slot.
        /// </summary>
        /// <param name="hallwaySlot">
        /// Hallway slot the <see cref="Amphipod"/> is currently situated in (in the range
        /// [0; <see cref="HallwaySlots"/> - 1]).
        /// </param>
        /// <param name="neighbor">
        /// The neighbor <see cref="State"/> if entering the target room was possible,
        /// otherwise <see langword="null"/>.
        /// </param>
        /// <param name="additionalEnergy">
        /// Additional energy required if entering the target room was possible, otherwise zero.
        /// </param>
        /// <returns>
        /// <see langword="True"/> if entering the target room was possible,
        /// otherwise <see langword="false"/>.
        /// </returns>
        private bool TryEnterTargetRoom(
            int hallwaySlot,
            [NotNullWhen(true)] out State? neighbor,
            out int additionalEnergy
        ) {
            Amphipod amphipod = slots[hallwaySlot];
            int targetRoom = AmphipodToTargetRoom[amphipod];
            int hallwaySlotAboveTargetRoom = HallwaySlotAboveRoom(targetRoom);
            if (IsHallwayBlocked(hallwaySlot, hallwaySlotAboveTargetRoom)) {
                neighbor = default;
                additionalEnergy = default;
                return false;
            }
            foreach (Amphipod other in slots.AsSpan(FirstRoomSlot(targetRoom), roomDepth)) {
                if ((other != Amphipod.None) && (other != amphipod)) {
                    neighbor = default;
                    additionalEnergy = default;
                    return false;
                }
            }
            Span<Amphipod> updatedSlots = [.. slots];
            updatedSlots[hallwaySlot] = Amphipod.None;
            int targetDepth = roomDepth - 1;
            while (updatedSlots[FirstRoomSlot(targetRoom) + targetDepth] != Amphipod.None) {
                targetDepth--;
            }
            updatedSlots[FirstRoomSlot(targetRoom) + targetDepth] = amphipod;
            neighbor = new State(updatedSlots);
            int steps = Math.Abs(hallwaySlotAboveTargetRoom - hallwaySlot) + targetDepth + 1;
            additionalEnergy = steps * AmphipodToEnergyFactor[amphipod];
            return true;
        }

        /// <summary>Tries to enter the hallway from a given room slot.</summary>
        /// <param name="room">
        /// Room the <see cref="Amphipod"/> is currently situated in (in the range
        /// [0; <see cref="Rooms"/> - 1]).
        /// </param>
        /// <param name="depth">
        /// Depth (or slot within the room) the <see cref="Amphipod"/> is situated in.
        /// </param>
        /// <returns>
        /// A sequence of neighbor states and required additional energies. Note that the sequence
        /// may be empty if entering the hallway was impossible.
        /// </returns>
        private IEnumerable<(State Neighbor, int AdditionalEnergy)> TryEnterHallway(
            int room,
            int depth
        ) {
            int slot = FirstRoomSlot(room) + depth;
            Amphipod amphipod = slots[slot];
            ReadOnlySpan<Amphipod> currentRoom = slots.AsSpan(FirstRoomSlot(room), roomDepth);
            bool isSettledIn = room == AmphipodToTargetRoom[amphipod];
            for (int i = 0; i < roomDepth; i++) {
                Amphipod other = currentRoom[i];
                isSettledIn &= (other == Amphipod.None) || (other == amphipod);
            }
            if (isSettledIn) {
                yield break;
            }
            for (int i = depth - 1; i >= 0; i--) {
                if (currentRoom[i] != Amphipod.None) {
                    yield break;
                }
            }
            foreach (int hallwayTargetSlot in HallwayTargetSlots) {
                if (slots[hallwayTargetSlot] == Amphipod.None) {
                    int hallwaySlotAboveCurrentRoom = HallwaySlotAboveRoom(room);
                    if (!IsHallwayBlocked(hallwaySlotAboveCurrentRoom, hallwayTargetSlot)) {
                        Span<Amphipod> updatedSlots = [.. slots];
                        updatedSlots[slot] = Amphipod.None;
                        updatedSlots[hallwayTargetSlot] = amphipod;
                        int steps = Math.Abs(hallwayTargetSlot - hallwaySlotAboveCurrentRoom)
                            + depth + 1;
                        int additionalEnergy = steps * AmphipodToEnergyFactor[amphipod];
                        yield return (new State(updatedSlots), additionalEnergy);
                    }
                }
            }
        }

        /// <summary>
        /// Returns a sequence of the neighbor states (and additional energies) reached by letting
        /// amphipods enter either the hallway or their target room if possible.
        /// </summary>
        /// <returns>A sequence of the neighbor states and additional energies.</returns>
        private IEnumerable<(State Neighbor, int AdditionalEnergy)> Neighbors() {
            for (int i = 0; i < HallwaySlots; i++) {
                if ((slots[i] != Amphipod.None)
                        && TryEnterTargetRoom(i, out State? neighbor, out int additionalEnergy)) {
                    yield return (neighbor, additionalEnergy);
                }
            }
            for (int room = 0; room < Rooms; room++) {
                for (int depth = 0; depth < roomDepth; depth++) {
                    if (slots[FirstRoomSlot(room) + depth] != Amphipod.None) {
                        foreach ((State neighbor, int additionalEnergy)
                                in TryEnterHallway(room, depth)) {
                            yield return (neighbor, additionalEnergy);
                        }
                    }
                }
            }
        }

        /// <summary>Determines if this <see cref="State"/> is organized.</summary>
        /// <remarks>
        /// In an organized <see cref="State"/>, all of the rooms are filled completely with the
        /// corresponding <see cref="Amphipod"/> type of that room.
        /// </remarks>
        /// <returns>
        /// <see langword="True"/> if this <see cref="State"/> is organized,
        /// otherwise <see langword="false"/>.
        /// </returns>
        private bool IsOrganized() {
            for (int room = 0; room < Rooms; room++) {
                foreach (Amphipod amphipod in slots.AsSpan(FirstRoomSlot(room), roomDepth)) {
                    if ((amphipod == Amphipod.None) || (room != AmphipodToTargetRoom[amphipod])) {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Calculates the least amount of energy required for organizing this <see cref="State"/>.
        /// </summary>
        /// <remarks>
        /// In an organized <see cref="State"/>, all of the rooms are filled completely with the
        /// corresponding <see cref="Amphipod"/> type of that room.
        /// </remarks>
        /// <returns>
        /// The least amount of energy required for organizing this <see cref="State"/>.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when this <see cref="State"/> could not be organized. This is only possible
        /// if the requirements of <see cref="Parse(IEnumerable{string})"/> were violated.
        /// </exception>
        public int LeastEnergyRequiredForOrganization() {
            Dictionary<State, int> requiredEnergy = new() { [this] = 0 };
            HashSet<State> visitedStates = [this];
            PriorityQueue<State, int> remainingStates = new([(this, 0)]);
            while (remainingStates.Count > 0) {
                State state = remainingStates.Dequeue();
                if (state.IsOrganized()) {
                    return requiredEnergy[state];
                }
                foreach ((State neighbor, int additionalEnergy) in state.Neighbors()) {
                    int newRequiredEnergy = requiredEnergy[state] + additionalEnergy;
                    if (!requiredEnergy.TryGetValue(neighbor, out int previouslyRequiredEnergy)
                            || (newRequiredEnergy < previouslyRequiredEnergy)) {
                        requiredEnergy[neighbor] = newRequiredEnergy;
                        if (visitedStates.Add(neighbor)) {
                            remainingStates.Enqueue(neighbor, newRequiredEnergy);
                        }
                    }
                }
            }
            throw new InvalidOperationException("Failed to reach an organized state.");
        }

        /// <summary>Determines if this <see cref="State"/> is equal to a given one.</summary>
        /// <param name="other">Other <see cref="State"/> to compare this instance to.</param>
        /// <returns>
        /// <see langword="True"/> if this <see cref="State"/> is equal to the given one,
        /// otherwise <see langword="false"/>.
        /// </returns>
        public bool Equals(State? other)
            => (other != null) && slots.SequenceEqual(other.slots)
                && (roomDepth == other.roomDepth);

        /// <summary>Determines if this <see cref="State"/> is equal to a given object.</summary>
        /// <param name="obj">Object to compare this instance to.</param>
        /// <returns>
        /// <see langword="True"/> if the given object is a <see cref="State"/> and equal to this
        /// instance, otherwise <see langword="false"/>.
        /// </returns>
        public override bool Equals(object? obj) => Equals(obj as State);

        /// <summary>Returns a hash code for this <see cref="State"/>.</summary>
        /// <returns>A hash code for this <see cref="State"/>.</returns>
        public override int GetHashCode() {
            HashCode result = new();
            foreach (Amphipod amphipod in slots) {
                result.Add(amphipod);
            }
            result.Add(roomDepth);
            return result.ToHashCode();
        }

    }

    private static readonly string InputFile = Path.Combine(
        AppContext.BaseDirectory,
        "resources",
        "input.txt"
    );

    /// <summary>Solves the <see cref="Amphipod"/> puzzle.</summary>
    /// <param name="textWriter"><see cref="TextWriter"/> to write the results to.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="textWriter"/> is <see langword="null"/>.
    /// </exception>
    internal static void Solve(TextWriter textWriter) {
        Guard.IsNotNull(textWriter);
        ImmutableArray<string> lines = [.. File.ReadLines(InputFile)];
        int requiredEnergyInitial = State.Parse(lines).LeastEnergyRequiredForOrganization();
        lines = [.. lines[..3], "  #D#C#B#A#", "  #D#B#A#C#", .. lines[3..]];
        int requiredEnergyExtended = State.Parse(lines).LeastEnergyRequiredForOrganization();
        textWriter.WriteLine(
            $"The least energy required for the initial situation is {requiredEnergyInitial}."
        );
        textWriter.WriteLine(
            $"The least energy required for the extended situation is {requiredEnergyExtended}."
        );
    }

    private static void Main(string[] args) {
        if (args.Contains("--benchmark")) {
            BenchmarkRunner.Run<Benchmark>();
        }
        else {
            Solve(Console.Out);
        }
    }

}