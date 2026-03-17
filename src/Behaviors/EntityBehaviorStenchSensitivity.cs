using System;
using System.Collections.Generic;
using System.Reflection;
using StenchMod.Config;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace StenchMod.Behaviors
{
    /// <summary>
    /// Server-side behavior attached to drifters and selected animals.
    /// Filters existing AI tasks so stinky players are harder or impossible to target
    /// without disabling unrelated seek behaviors such as hunting prey.
    /// </summary>
    public class EntityBehaviorStenchSensitivity : EntityBehavior
    {
        private const string AttrLevel = "stench:level";

        private static readonly BindingFlags AnyInstance =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly FieldInfo? SeekRangeField =
            typeof(AiTaskSeekEntity).GetField("seekingRange", AnyInstance);

        private static readonly FieldInfo? MeleeAttackRangeField =
            typeof(AiTaskMeleeAttackTargetingEntity).GetField("attackRange", AnyInstance);

        private static readonly FieldInfo? ThrowMaxDistField =
            typeof(AiTaskThrowAtEntity).GetField("maxDist", AnyInstance);

        private static readonly FieldInfo? ThrowTargetEntityField =
            typeof(AiTaskThrowAtEntity).GetField("targetEntity", AnyInstance);

        private static readonly PropertyInfo? ThrowTargetEntityProperty =
            typeof(AiTaskThrowAtEntity).GetProperty("TargetEntity", AnyInstance);

        private EntityBehaviorTaskAI? taskAi;
        private readonly Dictionary<object, TaskTargetState> trackedTasks = new();
        private bool isRelevantAnimal;
        private bool boundToTaskManager;

        private StenchConfig Config => StenchModSystem.Config;

        public EntityBehaviorStenchSensitivity(Entity entity) : base(entity) { }

        public override string PropertyName() => "stench.sensitivity";

        public override void Initialize(EntityProperties properties, JsonObject typeAttributes)
        {
            base.Initialize(properties, typeAttributes);

            if (entity.Api.Side != EnumAppSide.Server || entity is not EntityAgent)
                return;

            string entityPath = entity.Code?.Path ?? string.Empty;
            isRelevantAnimal = ContainsAny(entityPath, Config.AffectedAnimals);

            TryBindTaskManager();
        }

        public override void OnGameTick(float deltaTime)
        {
            base.OnGameTick(deltaTime);

            if (!boundToTaskManager && entity.Api.Side == EnumAppSide.Server)
            {
                TryBindTaskManager();
            }

            if (boundToTaskManager)
            {
                SuppressInvalidActiveTargets();
            }
        }

        private void TryBindTaskManager()
        {
            if (boundToTaskManager || entity is not EntityAgent)
                return;

            bool enabledForEntity =
                isRelevantAnimal
                && Config.EnableAnimalAI
                && !Config.EnableAnimalSeekingRangeModifier;

            if (!enabledForEntity)
                return;

            taskAi = entity.GetBehavior<EntityBehaviorTaskAI>();
            if (taskAi?.TaskManager == null)
                return;

            CacheTrackedTasks(taskAi.TaskManager);
            taskAi.TaskManager.OnShouldExecuteTask += OnShouldExecuteTask;
            boundToTaskManager = true;
        }

        private void CacheTrackedTasks(AiTaskManager manager)
        {
            trackedTasks.Clear();

            foreach (object task in manager.AllTasks)
            {
                TaskTargetState? state = CreateState(task);
                if (state != null)
                {
                    trackedTasks[task] = state;
                }
            }
        }

        private TaskTargetState? CreateState(object task)
        {
            Type taskType = task.GetType();
            bool supportedTask =
                task is AiTaskSeekEntity
                || task is AiTaskMeleeAttackTargetingEntity
                || task is AiTaskThrowAtEntity;

            if (!supportedTask)
                return null;

            FieldInfo? exactField = FindFieldRecursive(taskType, "targetEntityCodesExact");
            FieldInfo? beginsField = FindFieldRecursive(taskType, "targetEntityCodesBeginsWith");

            string[] exactWithPlayer = CloneCodes(exactField?.GetValue(task) as string[]);
            string[] beginsWithPlayer = CloneCodes(beginsField?.GetValue(task) as string[]);

            bool targetsPlayers = ContainsPlayerCode(exactWithPlayer) || ContainsPlayerCode(beginsWithPlayer);
            if (!targetsPlayers)
                return null;

            return new TaskTargetState(
                exactField,
                beginsField,
                exactWithPlayer,
                beginsWithPlayer
            );
        }

        private bool OnShouldExecuteTask(IAiTask task)
        {
            if (!trackedTasks.TryGetValue(task, out TaskTargetState? state))
                return true;

            if (task is AiTaskSeekEntity seekTask)
            {
                return EvaluateSeekTask(seekTask, state);
            }

            if (task is AiTaskMeleeAttackTargetingEntity meleeTask)
            {
                return EvaluateMeleeTask(meleeTask, state);
            }

            if (task is AiTaskThrowAtEntity throwTask)
            {
                return EvaluateThrowTask(throwTask, state);
            }

            return true;
        }

        private void SuppressInvalidActiveTargets()
        {
            foreach ((object task, TaskTargetState state) in trackedTasks)
            {
                if (task is AiTaskSeekEntity seekTask)
                {
                    float baseSeekRange = GetBaseSeekRange(seekTask);
                    if (seekTask.targetEntity is EntityPlayer activePlayer
                        && !ShouldTargetPlayer(activePlayer, baseSeekRange, false))
                    {
                        SetPlayerTargetEnabled(seekTask, state, false);
                        seekTask.targetEntity = null;
                    }

                    continue;
                }

                if (task is AiTaskMeleeAttackTargetingEntity meleeTask)
                {
                    float meleeRange = GetBaseMeleeRange(meleeTask);
                    if (meleeTask.TargetEntity is EntityPlayer player
                        && !ShouldTargetPlayer(player, meleeRange, false))
                    {
                        SetPlayerTargetEnabled(meleeTask, state, false);
                        meleeTask.targetEntity = null;
                    }

                    continue;
                }

                if (task is AiTaskThrowAtEntity throwTask)
                {
                    float baseThrowRange = GetBaseThrowRange(throwTask);
                    Entity? targetEntity = GetThrowTargetEntity(throwTask);
                    if (targetEntity is EntityPlayer activePlayer
                        && !ShouldTargetPlayer(activePlayer, baseThrowRange, false))
                    {
                        SetPlayerTargetEnabled(throwTask, state, false);
                        SetThrowTargetEntity(throwTask, null);
                    }
                }
            }
        }

        private bool EvaluateSeekTask(AiTaskSeekEntity seekTask, TaskTargetState state)
        {
            float baseSeekRange = GetBaseSeekRange(seekTask);
            if (baseSeekRange <= 0f)
            {
                SetPlayerTargetEnabled(seekTask, state, true);
                return true;
            }

            if (seekTask.targetEntity is EntityPlayer activePlayer
                && !ShouldTargetPlayer(activePlayer, baseSeekRange, false))
            {
                seekTask.targetEntity = null;
            }

            if (ShouldTargetAnyPlayer(baseSeekRange))
            {
                SetPlayerTargetEnabled(seekTask, state, true);
                return true;
            }

            SetPlayerTargetEnabled(seekTask, state, false);
            seekTask.targetEntity = null;
            return state.HasNonPlayerTargets;
        }

        private bool EvaluateMeleeTask(AiTaskMeleeAttackTargetingEntity meleeTask, TaskTargetState state)
        {
            float meleeRange = GetBaseMeleeRange(meleeTask);

            if (meleeTask.TargetEntity is not EntityPlayer player || !player.Alive)
            {
                SetPlayerTargetEnabled(meleeTask, state, true);
                return true;
            }

            bool shouldTarget = ShouldTargetPlayer(player, meleeRange, false);

            SetPlayerTargetEnabled(meleeTask, state, shouldTarget);

            if (!shouldTarget)
            {
                meleeTask.targetEntity = null;
                return state.HasNonPlayerTargets;
            }

            return true;
        }

        private bool EvaluateThrowTask(AiTaskThrowAtEntity throwTask, TaskTargetState state)
        {
            float baseThrowRange = GetBaseThrowRange(throwTask);
            if (baseThrowRange <= 0f)
            {
                SetPlayerTargetEnabled(throwTask, state, true);
                return true;
            }

            Entity? targetEntity = GetThrowTargetEntity(throwTask);
            if (targetEntity is EntityPlayer activePlayer
                && !ShouldTargetPlayer(activePlayer, baseThrowRange, false))
            {
                SetThrowTargetEntity(throwTask, null);
            }

            if (ShouldTargetAnyPlayer(baseThrowRange))
            {
                SetPlayerTargetEnabled(throwTask, state, true);
                return true;
            }

            SetPlayerTargetEnabled(throwTask, state, false);
            SetThrowTargetEntity(throwTask, null);
            return state.HasNonPlayerTargets;
        }

        private bool ShouldTargetAnyPlayer(float baseSeekRange)
        {
            foreach (IPlayer playerRef in entity.World.AllPlayers)
            {
                if (playerRef?.Entity is not EntityPlayer player || !player.Alive)
                    continue;

                if (ShouldTargetPlayer(player, baseSeekRange, true))
                {
                    return true;
                }
            }

            return false;
        }

        private static float GetBaseSeekRange(AiTaskSeekEntity seekTask)
        {
            if (SeekRangeField?.GetValue(seekTask) is float range && range > 0f)
                return range;

            return seekTask.NowSeekRange;
        }

        private static float GetBaseThrowRange(AiTaskThrowAtEntity throwTask)
        {
            if (ThrowMaxDistField?.GetValue(throwTask) is float maxDist && maxDist > 0f)
                return maxDist;

            return 0f;
        }

        private static float GetBaseMeleeRange(AiTaskMeleeAttackTargetingEntity meleeTask)
        {
            if (MeleeAttackRangeField?.GetValue(meleeTask) is float attackRange && attackRange > 0f)
                return attackRange;

            return 0f;
        }

        private bool ShouldTargetPlayer(EntityPlayer player, float baseSeekRange, bool applyRandomIgnore)
        {
            float[] rangeMultipliers = Config.AnimalRangeMultipliers;
            float[] ignoreChances = Config.AnimalIgnoreChances;

            int maxIdx = Math.Min(rangeMultipliers.Length, ignoreChances.Length) - 1;
            if (maxIdx < 0)
                return true;

            int level = GetPlayerStenchLevel(player);
            if (level <= 2)
                return true;

            int idx = Math.Min(level - 1, maxIdx);
            float effectiveRange = baseSeekRange * rangeMultipliers[idx];
            double distance = entity.Pos.DistanceTo(player.Pos.XYZ);

            if (rangeMultipliers[idx] <= 0f || distance > effectiveRange)
                return false;

            if (ignoreChances[idx] >= 1f)
                return false;

            if (applyRandomIgnore && ignoreChances[idx] > 0f && entity.World.Rand.NextDouble() < ignoreChances[idx])
                return false;

            return true;
        }

        private static Entity? GetThrowTargetEntity(AiTaskThrowAtEntity throwTask)
        {
            if (ThrowTargetEntityProperty?.GetValue(throwTask) is Entity entity)
                return entity;

            return ThrowTargetEntityField?.GetValue(throwTask) as Entity;
        }

        private static void SetThrowTargetEntity(AiTaskThrowAtEntity throwTask, Entity? target)
        {
            ThrowTargetEntityField?.SetValue(throwTask, target);

            if (ThrowTargetEntityProperty?.CanWrite == true)
            {
                ThrowTargetEntityProperty.SetValue(throwTask, target);
            }
        }

        private static void SetPlayerTargetEnabled(object task, TaskTargetState state, bool enabled)
        {
            string[] exactCodes = enabled ? state.ExactWithPlayer : state.ExactWithoutPlayer;
            string[] beginsWithCodes = enabled ? state.BeginsWithPlayer : state.BeginsWithWithoutPlayer;

            state.ExactField?.SetValue(task, exactCodes);
            state.BeginsWithField?.SetValue(task, beginsWithCodes);
        }

        private static int GetPlayerStenchLevel(EntityPlayer player)
        {
            EntityBehaviorStench? behavior = player.GetBehavior<EntityBehaviorStench>();
            if (behavior != null)
            {
                return Math.Clamp(behavior.GetCurrentLevel(), 1, 5);
            }

            return Math.Clamp(player.WatchedAttributes.GetInt(AttrLevel, 1), 1, 5);
        }

        private static bool ContainsAny(string source, List<string> values)
        {
            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value)
                    && source.Contains(value, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string[] CloneCodes(string[]? source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<string>();

            string[] clone = new string[source.Length];
            Array.Copy(source, clone, source.Length);
            return clone;
        }

        private static bool ContainsPlayerCode(string[] codes)
        {
            foreach (string code in codes)
            {
                if (IsPlayerCode(code))
                    return true;
            }

            return false;
        }

        private static bool IsPlayerCode(string code)
        {
            return !string.IsNullOrWhiteSpace(code)
                && code.StartsWith("player", StringComparison.OrdinalIgnoreCase);
        }

        private static FieldInfo? FindFieldRecursive(Type type, string name)
        {
            Type? current = type;
            while (current != null)
            {
                FieldInfo? field = current.GetField(name, AnyInstance);
                if (field != null)
                {
                    return field;
                }

                current = current.BaseType;
            }

            return null;
        }

        private void LogTaskSummary(AiTaskManager manager)
        {
            List<string> taskTypes = new();
            foreach (object task in manager.AllTasks)
            {
                taskTypes.Add(task.GetType().FullName ?? task.GetType().Name);
            }

            entity.Api.Logger.Notification(
                "[stench] [StenchSensitivity] Bound to {0} with {1} tracked target tasks. Runtime task types: {2}",
                $"{entity.Code?.Path ?? "<unknown>"}#{entity.EntityId}",
                trackedTasks.Count,
                string.Join(", ", taskTypes)
            );
        }

        private sealed class TaskTargetState
        {
            public readonly FieldInfo? ExactField;
            public readonly FieldInfo? BeginsWithField;
            public readonly string[] ExactWithPlayer;
            public readonly string[] BeginsWithPlayer;
            public readonly string[] ExactWithoutPlayer;
            public readonly string[] BeginsWithWithoutPlayer;
            public readonly bool HasNonPlayerTargets;

            public TaskTargetState(
                FieldInfo? exactField,
                FieldInfo? beginsWithField,
                string[] exactWithPlayer,
                string[] beginsWithPlayer)
            {
                ExactField = exactField;
                BeginsWithField = beginsWithField;
                ExactWithPlayer = exactWithPlayer;
                BeginsWithPlayer = beginsWithPlayer;
                ExactWithoutPlayer = FilterOutPlayerCodes(exactWithPlayer);
                BeginsWithWithoutPlayer = FilterOutPlayerCodes(beginsWithPlayer);
                HasNonPlayerTargets =
                    ExactWithoutPlayer.Length > 0 || BeginsWithWithoutPlayer.Length > 0;
            }

            private static string[] FilterOutPlayerCodes(string[] source)
            {
                List<string> filtered = new();

                foreach (string code in source)
                {
                    if (!IsPlayerCode(code))
                    {
                        filtered.Add(code);
                    }
                }

                return filtered.ToArray();
            }
        }
    }
}
