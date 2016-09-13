﻿using System.Collections.Generic;
using System.Numerics;

namespace LeagueSandbox.GameServer.Logic.GameObjects
{
    public enum MinionSpawnPosition : uint
    {
        SPAWN_BLUE_TOP = 0xeb364c40,
        SPAWN_BLUE_BOT = 0x53b83640,
        SPAWN_BLUE_MID = 0xb7717140,
        SPAWN_RED_TOP = 0xe647d540,
        SPAWN_RED_BOT = 0x5ec9af40,
        SPAWN_RED_MID = 0xba00e840
    };

    public enum MinionSpawnType : byte
    {
        MINION_TYPE_MELEE = 0x00,
        MINION_TYPE_CASTER = 0x03,
        MINION_TYPE_CANNON = 0x02,
        MINION_TYPE_SUPER = 0x01
    };
    public class Minion : Unit
    {
        /**
 * Const waypoints that define the minion's route
 */
        protected List<Vector2> mainWaypoints;
        protected int curMainWaypoint = 0;
        public MinionSpawnPosition SpawnPosition { get; private set; }
        protected MinionSpawnType minionType;
        protected bool _AIPaused;

        public Minion(
            MinionSpawnType type,
            MinionSpawnPosition position,
            List<Vector2> mainWaypoints,
            uint netId = 0
        ) : base("", new MinionStats(), 40, 0, 0, 1100, netId)
        {
            this.minionType = type;
            this.SpawnPosition = position;
            this.mainWaypoints = mainWaypoints;
            this.curMainWaypoint = 0;
            _AIPaused = false;

            var spawnSpecifics = _game.GetMap().GetMinionSpawnPosition(SpawnPosition);
            Team = spawnSpecifics.Item1;
            setPosition(spawnSpecifics.Item2.X, spawnSpecifics.Item2.Y);

            _game.GetMap().SetMinionStats(this); // Let the map decide how strong this minion has to be.

            string minionModel = "";
            if (spawnSpecifics.Item1 == 0) // If we're the blue side
                minionModel += "Blue_Minion_"; // make it a blue minion
            else
                minionModel += "Red_Minion_"; // otherwise make it a red minion

            // Finish model name with type
            if (type == MinionSpawnType.MINION_TYPE_MELEE)
                minionModel += "Basic";
            else if (type == MinionSpawnType.MINION_TYPE_CASTER)
                minionModel += "Wizard";
            else if (type == MinionSpawnType.MINION_TYPE_CANNON)
                minionModel += "MechCannon";
            else
                minionModel += "MechMelee";

            // Set model
            Model = minionModel;


            // If we have lane path instructions from the map
            if (mainWaypoints.Count > 0)
            {
                // Follow these instructions
                setWaypoints(new List<Vector2> { mainWaypoints[0], mainWaypoints[1] });
            }
            else
            {
                // Otherwise path to own position. (Stand still)
                setWaypoints(new List<Vector2> { new Vector2(X, Y), new Vector2(X, Y) });
            }

            setMoveOrder(MoveOrder.MOVE_ORDER_ATTACKMOVE);
        }

        public Minion(
            MinionSpawnType type,
            MinionSpawnPosition position,
            uint netId = 0
        ) : this(type, position, new List<Vector2>(), netId)
        {

        }

        public MinionSpawnType getType()
        {
            return minionType;
        }

        public void PauseAI(bool b)
        {
            _AIPaused = b;
        }

        public override void update(long diff)
        {
            base.update(diff);

            if (!isDead())
            {
                if (IsDashing)
                    return;
                if (!_AIPaused)
                {
                    if (scanForTargets())     // returns true if we have a target
                        keepFocussingTarget(); // fight target
                    else
                        walkToDestination(); // walk to destination (or target)
                }
            }
        }

        public override void onCollision(GameObject a_Collider)
        {
            if (a_Collider == targetUnit) // If we're colliding with the target, don't do anything.
                return;

            if (targetUnit != null)
            {
                // Thread this?
                //Path newPath = Pathfinder::getPath(getPosition(), targetUnit->getPosition());
                //if (newPath.error == PATH_ERROR_NONE)
                //setWaypoints(newPath.getWaypoints());
            }
        }

        public override bool isInDistress()
        {
            return distressCause != null;
        }

        // AI tasks
        protected bool scanForTargets()
        {
            Unit nextTarget = null;
            double nextTargetPriority = 9e5;

            var objects = _game.GetMap().GetObjects();
            foreach (var it in objects)
            {
                var u = it.Value as Unit;

                // Targets have to be:
                if (u == null ||                                    // a unit
                     u.isDead() ||                                  // alive
                     u.Team == Team ||                    // not on our team
                     distanceWith(u) > DETECT_RANGE ||              // in range
                     !_game.GetMap().TeamHasVisionOn(Team, u))       // visible to this minion
                    continue;                                       // If not, look for something else

                var priority = classifyTarget(u);  // get the priority.
                if (priority < nextTargetPriority) // if the priority is lower than the target we checked previously
                {
                    nextTarget = u;                // make him a potential target.
                    nextTargetPriority = priority;
                }
            }

            if (nextTarget != null) // If we have a target
            {
                setTargetUnit(nextTarget); // Set the new target and refresh waypoints
                _game.PacketNotifier.notifySetTarget(this, nextTarget);
                return true;
            }
            return false;
        }
        protected void walkToDestination()
        {
            if (mainWaypoints.Count > curMainWaypoint + 1)
            {
                if ((waypoints.Count == 1) || (curWaypoint == 2 && ++curMainWaypoint < mainWaypoints.Count))
                {
                    //CORE_INFO("Minion reached a point! Going to %f; %f", mainWaypoints[curMainWaypoint].X, mainWaypoints[curMainWaypoint].Y);
                    List<Vector2> newWaypoints = new List<Vector2> { new Vector2(X, Y), mainWaypoints[curMainWaypoint] };
                    setWaypoints(newWaypoints);
                }
            }
        }
        protected void keepFocussingTarget()
        {
            if (isAttacking && (targetUnit == null || distanceWith(targetUnit) > stats.Range.Total))
            // If target is dead or out of range
            {
                _game.PacketNotifier.notifyStopAutoAttack(this);
                isAttacking = false;
            }
        }
    }
}
