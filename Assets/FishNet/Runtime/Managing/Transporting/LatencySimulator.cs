using FishNet.Connection;
using FishNet.Transporting;
using FishNet.Utility.Performance;
using System;
using System.Collections.Generic;
using UnityEngine;

//Thanks to TiToMoskito originally creating this as a Transport.
//https://github.com/TiToMoskito/FishyLatency
namespace FishNet.Managing.Transporting
{
    [System.Serializable]
    public class LatencySimulator
    {
        #region Types.
        /// <summary>
        /// A message affected by latency.
        /// </summary>
        private struct Message
        {
            public readonly int ConnectionId;
            public readonly byte[] Data;
            public readonly int Length;
            public readonly float SendTime;

            public Message(int connectionId, ArraySegment<byte> segment, float latency)
            {
                this.ConnectionId = connectionId;
                this.SendTime = (Time.unscaledTime + latency);
                this.Length = segment.Count;
                this.Data = ByteArrayPool.Retrieve(this.Length);
                Buffer.BlockCopy(segment.Array, segment.Offset, this.Data, 0, this.Length);
            }

            public ArraySegment<byte> GetSegment()
            {
                return new ArraySegment<byte>(Data, 0, Length);
            }
        }
        #endregion

        #region Internal.
        /// <summary>
        /// True if latency can be simulated.
        /// </summary>
        internal bool CanSimulate => (GetEnabled() && (GetLatency() > 0 || GetPacketLost() > 0 || GetOutOfOrder() > 0));
        #endregion

        #region Serialized
        [Header("Settings")]
        /// <summary>
        /// 
        /// </summary>
        [Tooltip("True if latency simulator is enabled.")]
        [SerializeField]
        private bool _enabled;
        /// <summary>
        /// Gets the enabled value of simulator.
        /// </summary>
        public bool GetEnabled() => _enabled;
        /// <summary>
        /// Sets the enabled value of simulator.
        /// </summary>
        /// <param name="value">New value.</param>
        public void SetEnabled(bool value)
        {
            if (value == _enabled)
                return;

            _enabled = value;
            Reset();
        }
        /// <summary>
        /// 
        /// </summary>
        [Tooltip("True to add latency on clientHost as well.")]
        [SerializeField]
        private bool _simulateHost = true;
        
        /// <summary>
        /// Milliseconds to add between packets. When acting as host this value will be doubled. Added latency will be a minimum of tick rate.
        /// </summary>
        [Tooltip("Milliseconds to add between packets. When acting as host this value will be doubled. Added latency will be a minimum of tick rate.")]
        [Range(0, 60000)]
        [SerializeField]
        private long _latency = 0;
        /// <summary>
        /// Gets the latency value.
        /// </summary>
        /// <returns></returns>
        public long GetLatency() => _latency;
        /// <summary>
        /// Sets a new latency value.
        /// </summary>
        /// <param name="value">Latency as milliseconds.</param>
        public void SetLatency(long value) => _latency = value;
        
        /// <summary>
        /// Interval between latency bursts in seconds.
        /// </summary>
        [Tooltip("Interval at which latency bursts occur. 0=no bursts")]
        [Range(0, 600)]
        [SerializeField]
        private double _latencyBurstInterval = 0;
        /// <summary>
        /// Gets latency burst interval in seconds. 0f is disabled.
        /// </summary>
        /// <returns></returns>
        public double GetLatencyBurstInterval() => _latencyBurstInterval;
        /// <summary>
        /// Sets latency burst interval in seconds. 0f is disabled.
        /// </summary>
        /// <param name="value">New Value.</param>
        public void SetLatencyBurstInterval(double value) => _latencyBurstInterval = value;
        
        /// <summary>
        /// Length of latency bursts in seconds.
        /// </summary>
        [Tooltip("Length of latency bursts in seconds. 0 = no bursts")]
        [Range(0, 5)]
        [SerializeField]
        private double _latencyBurstLength = 0;
        /// <summary>
        /// Gets latency burst period length in seconds. 0f is disabled.
        /// </summary>
        /// <returns></returns>
        public double GetLatencyBurstLength() => _latencyBurstLength;
        /// <summary>
        /// Sets latency burst period length in seconds. 0f is disabled.
        /// </summary>
        /// <param name="value">New Value.</param>
        public void SetLatencyBurstLength(double value) => _latencyBurstLength = value;
        
        /// <summary>
        /// Percentage of packets which should drop during a burst.
        /// </summary>
        [Tooltip("Percentage of packets which should drop during a burst.")]
        [Range(0, 1)]
        [SerializeField]
        private double _packetLossBurst = 0;
        /// <summary>
        /// Gets packet loss chance during a burst. 1f is a 100% chance to occur.
        /// </summary>
        /// <returns></returns>
        public double GetPacketLossBurst() => _packetLossBurst;
        /// <summary>
        /// Sets packet loss chance during a burst. 1f is a 100% chance to occur.
        /// </summary>
        /// <param name="value">New Value.</param>
        public void SetPacketLossBurst(double value) => _packetLossBurst = value;

        [Header("Unreliable")]
        /// <summary>
        /// Percentage of unreliable packets which should arrive out of order.
        /// </summary>
        [Tooltip("Percentage of unreliable packets which should arrive out of order.")]
        [Range(0f, 1f)]
        [SerializeField]
        private double _outOfOrder = 0;
        /// <summary>
        /// Out of order chance, 1f is a 100% chance to occur.
        /// </summary>
        /// <returns></returns>
        public double GetOutOfOrder() => _outOfOrder;
        /// <summary>
        /// Sets out of order chance. 1f is a 100% chance to occur.
        /// </summary>
        /// <param name="value">New Value.</param>
        public void SetOutOfOrder(double value) => _outOfOrder = value;
        
        /// <summary>
        /// Ticks of jitter to add between packets. Will pick a random value between 0->this value
        /// </summary>
        [Tooltip("Ticks of jitter to add between packets. Will pick a random value between 0->this value before sending the next group.")]
        [Range(0, 60)]
        [SerializeField]
        private long _jitter = 0;
        /// <summary>
        /// Gets the jitter value.
        /// </summary>
        /// <returns></returns>
        public long GetJitter() => _jitter;
        /// <summary>
        /// Sets a new jitter value.
        /// </summary>
        /// <param name="value">Latency as milliseconds.</param>
        public void SetJitter(long value) => _jitter = value;
        
        /// <summary>
        /// Packet loss and ping variability.
        /// </summary>
        [Tooltip("Percentage of packet loss and ping variability. Will affect base + burst + ping by consider the loss/ping to a random in the range like _packetLoss-(_packetLoss*_variability) -> _packetLoss+(_packetLoss*_variability)")]
        [Range(0, 1)]
        [SerializeField]
        private double _variability = 0;
        /// <summary>
        /// Gets loss and ping variability.
        /// </summary>
        /// <returns></returns>
        public double GetVariability() => _variability;
        /// <summary>
        /// Sets loss and ping variability.
        /// </summary>
        /// <param name="value">New Value.</param>
        public void SetVariability(double value) => _variability = value;
        
        /// <summary>
        /// Percentage of packets which should drop.
        /// </summary>
        [Tooltip("Percentage of packets which should drop.")]
        [Range(0, 1)]
        [SerializeField]
        private double _packetLoss = 0;
        /// <summary>
        /// Gets packet loss chance. 1f is a 100% chance to occur.
        /// </summary>
        /// <returns></returns>
        public double GetPacketLost() => _packetLoss;
        /// <summary>
        /// Sets packet loss chance. 1f is a 100% chance to occur.
        /// </summary>
        /// <param name="value">New Value.</param>
        public void SetPacketLoss(double value) => _packetLoss = value;
        
        /// <summary>
        /// Interval between packet loss bursts in seconds.
        /// </summary>
        [Tooltip("Interval at which packet loss bursts occur. 0=no bursts")]
        [Range(0, 600)]
        [SerializeField]
        private double _packetLossBurstInterval = 0;
        /// <summary>
        /// Gets packet loss burst interval in seconds. 0f is disabled.
        /// </summary>
        /// <returns></returns>
        public double GetPacketLossBurstInterval() => _packetLossBurstInterval;
        /// <summary>
        /// Sets packet loss burst interval in seconds. 0f is disabled.
        /// </summary>
        /// <param name="value">New Value.</param>
        public void SetPacketLossBurstInterval(double value) => _packetLossBurstInterval = value;
        
        /// <summary>
        /// Length of packet loss bursts in seconds.
        /// </summary>
        [Tooltip("Length of packet loss bursts in seconds. 0 = no bursts")]
        [Range(0, 5)]
        [SerializeField]
        private double _packetLossBurstLength = 0;
        /// <summary>
        /// Gets packet loss burst period length in seconds. 0f is disabled.
        /// </summary>
        /// <returns></returns>
        public double GetPacketLossBurstLength() => _packetLossBurstLength;
        /// <summary>
        /// Sets packet loss burst period length in seconds. 0f is disabled.
        /// </summary>
        /// <param name="value">New Value.</param>
        public void SetPacketLossBurstLength(double value) => _packetLossBurstLength = value;
        
        /// <summary>
        /// Percentage of packets which should drop during a burst.
        /// </summary>
        [Tooltip("Percentage of packets which should drop during a burst.")]
        [Range(0, 1)]
        [SerializeField]
        private double _packetLossBurst = 0;
        /// <summary>
        /// Gets packet loss chance during a burst. 1f is a 100% chance to occur.
        /// </summary>
        /// <returns></returns>
        public double GetPacketLossBurst() => _packetLossBurst;
        /// <summary>
        /// Sets packet loss chance during a burst. 1f is a 100% chance to occur.
        /// </summary>
        /// <param name="value">New Value.</param>
        public void SetPacketLossBurst(double value) => _packetLossBurst = value;
        #endregion

        #region Private
        /// <summary>
        /// Transport to send data on.
        /// </summary>
        private Transport _transport;
        /// <summary>
        /// Reliable messages to the server.
        /// </summary>
        private List<Message> _toServerReliable = new List<Message>();
        /// <summary>
        /// Unreliable messages to the server.
        /// </summary>
        private List<Message> _toServerUnreliable = new List<Message>();
        /// <summary>
        /// Reliable messages to clients.
        /// </summary>
        private List<Message> _toClientReliable = new List<Message>();
        /// <summary>
        /// Unreliable messages to clients.
        /// </summary>
        private List<Message> _toClientUnreliable = new List<Message>();
        /// <summary>
        /// NetworkManager for this instance.
        /// </summary>
        private NetworkManager _networkManager;
        /// <summary>
        /// Used to generate chances of latency.
        /// </summary>
        private readonly System.Random _random = new System.Random();
        #endregion

        #region Initialization and Unity
        public void Initialize(NetworkManager manager, Transport transport)
        {
            _networkManager = manager;
            _transport = transport;
        }
        #endregion        

        /// <summary>
        /// Stops both client and server.
        /// </summary>
        public void Reset()
        {
            bool enabled = GetEnabled();
            if (_transport != null && enabled)
            { 
                IterateAndStore(_toServerReliable);
                IterateAndStore(_toServerUnreliable);
                IterateAndStore(_toClientReliable);
                IterateAndStore(_toClientUnreliable);
            }

            void IterateAndStore(List<Message> messages)
            {
                foreach (Message m in messages)
                {
                    _transport.SendToServer((byte)Channel.Reliable, m.GetSegment());
                    ByteArrayPool.Store(m.Data);
                }
            }

            _toServerReliable.Clear();
            _toServerUnreliable.Clear();
            _toClientReliable.Clear();
            _toClientUnreliable.Clear();
        }

        /// <summary>
        /// Removes pending or held packets for a connection.
        /// </summary>
        /// <param name="conn">Connection to remove pending packets for.</param>
        public void RemovePendingForConnection(int connectionId)
        {
            RemoveFromCollection(_toServerUnreliable);
            RemoveFromCollection(_toServerUnreliable);
            RemoveFromCollection(_toClientReliable);
            RemoveFromCollection(_toClientUnreliable);

            void RemoveFromCollection(List<Message> c)
            {
                for (int i = 0; i < c.Count; i++)
                {
                    if (c[i].ConnectionId == connectionId)
                    {
                        c.RemoveAt(i);
                        i--;
                    }
                }
            }
        }

        #region Simulation

        private float _NextLatencyBurstStart = -1f;
        /// <summary>
        /// Returns long latency as a float.
        /// </summary>
        /// <param name="ms"></param>
        /// <returns></returns>
        private float GetLatencyAsFloat()
        {
            var now = Time.unscaledTimeAsDouble;
            // see if we're configured for burst latency loss and haven't set the next time yet 
            if (_NextLatencyBurstStart < 0 && _latencyBurstInterval > 0 && _latencyBurstLength > 0 && _latencyBurst > 0) {
                var nextInterval = ComputeVariabilityForDouble(_latencyBurstInterval);
                _NextLatencyBurstStart = now + nextInterval;
            }

            if (now > _NextLatencyBurstStart) {
                if (now < _NextLatencyBurstStart + _latencyBurstLength) {
                    // inside the burst period
                    var lb = ComputeVariabilityForDouble(_latencyBurst);
                    return (float)(lb / 1000f);
                } else {
                    // compute the next burst interval start time
                    _NextLatencyBurstStart += _latencyBurstInterval;
                }
            }
            // not a burst period
            var lat = ComputeVariabilityForDouble(_latency);
            return (float)(lat / 1000f);
        }

        /// <summary>
        /// Adds a packet for simulation.
        /// </summary>
        public void AddOutgoing(byte channelId, ArraySegment<byte> segment, bool toServer = true, int connectionId = -1)
        {
            /* If to not simulate for host see if this packet
             * should be sent normally. */
            if (!_simulateHost && _networkManager != null && _networkManager.IsHost)
            {
                /* If going to the server and is host then
                 * it must be sent from clientHost. */
                if (toServer)
                {
                    _transport.SendToServer(channelId, segment);
                    return;
                }
                //Not to server, see if going to clientHost.
                else
                {
                    //If connId is the same as clientHost id.
                    if (_networkManager.ClientManager.Connection.ClientId == connectionId)
                    {
                        _transport.SendToClient(channelId, segment, connectionId);
                        return;
                    }
                }
            }

            List<Message> collection;
            Channel c = (Channel)channelId;

            if (toServer)
                collection = (c == Channel.Reliable) ? _toServerReliable : _toServerUnreliable;
            else
                collection = (c == Channel.Reliable) ? _toClientReliable : _toClientUnreliable;

            float latency = GetLatencyAsFloat();
            //If dropping check to add extra latency if reliable, or discard if not.
            if (DropPacket())
            {
                if (c == Channel.Reliable)
                {
                    latency += (latency * 0.3f); //add extra for resend.
                }
                //If not reliable then return the segment array to pool.
                else
                {
                    return;
                }
            }

            Message msg = new Message(connectionId, segment, latency);
            int count = collection.Count;
            if (c == Channel.Unreliable && count > 0 && OutOfOrderPacket(c))
                collection.Insert(count - 1, msg);
            else
                collection.Add(msg);
        }

        // used to track jitter simulation
        private int _NextSendTick = -1;

        /// <summary>
        /// Simulates pending outgoing packets.
        /// </summary>
        /// <param name="toServer">True if sending to the server.</param>
        public void IterateOutgoing(bool toServer)
        {
            if (_transport == null)
            {
                Reset();
                return;
            }

            if (--_NextSendTick > 0) {
                return;
            }
            
            if (_NextSendTick <= 0) {
                _NextSendTick = Mathf.RoundToInt(_random.NextDouble() * _jitter);
            }

            if (toServer)
            {
                IterateCollection(_toServerReliable, Channel.Reliable);
                IterateCollection(_toServerUnreliable, Channel.Unreliable);
            }
            else
            {
                IterateCollection(_toClientReliable, Channel.Reliable);
                IterateCollection(_toClientUnreliable, Channel.Unreliable);
            }

            void IterateCollection(List<Message> collection, Channel channel)
            {
                byte cByte = (byte)channel;
                float unscaledTime = Time.unscaledTime;

                int count = collection.Count;
                int iterations = 0;
                for (int i = 0; i < count; i++)
                {
                    Message msg = collection[i];
                    //Not enough time has passed.
                    if (unscaledTime < msg.SendTime)
                        break;

                    if (toServer)
                        _transport.SendToServer(cByte, msg.GetSegment());
                    else
                        _transport.SendToClient(cByte, msg.GetSegment(), msg.ConnectionId);

                    iterations++;
                }

                if (iterations > 0)
                {
                    for (int i = 0; i < iterations; i++)
                        ByteArrayPool.Store(collection[i].Data);
                    collection.RemoveRange(0, iterations);
                }
            }

            _transport.IterateOutgoing(toServer);
        }

        private double ComputeVariabilityForDouble(double value) {
            var minValue = value - (value * _variability);
            var maxValue = value + (value * _variability);
            return _random.NextDouble() * (maxValue - minValue) + minValue;
        }

        private double _NextPacketLossBurstStart = -1f;

        /// <summary>
        /// Returns if a packet should drop.
        /// </summary>
        /// <returns></returns>
        private bool DropPacket()
        {
            var now = Time.unscaledTimeAsDouble;
            // see if we're configured for burst packet loss and haven't set the next time yet 
            if (_NextPacketLossBurstStart < 0 && _packetLossBurstInterval > 0 && _packetLossBurstLength > 0 && _packetLossBurst > 0) {
                var nextInterval = ComputeVariabilityForDouble(_packetLossBurstInterval);
                _NextPacketLossBurstStart = now + nextInterval;
            }

            if (now > _NextPacketLossBurstStart) {
                if (now < _NextPacketLossBurstStart + _packetLossBurstLength) {
                    // inside the burst period
                    var plBurst = ComputeVariabilityForDouble(_packetLossBurst);
                    return (_packetLossBurst > 0d && (_random.NextDouble() < plBurst));
                } else {
                    // compute the next burst interval start time
                    _NextPacketLossBurstStart += _packetLossBurstInterval;
                }
            }
            // not a burst period
            var pl = ComputeVariabilityForDouble(_packetLoss);
            return (_packetLoss > 0d && (_random.NextDouble() < pl));
        }

        /// <summary>
        /// Returns if a packet should be out of order.
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        private bool OutOfOrderPacket(Channel c)
        {
            if (c == Channel.Reliable)
                return false;

            return (_outOfOrder > 0d && (_random.NextDouble() < _outOfOrder));
        }
        #endregion
    }
}

