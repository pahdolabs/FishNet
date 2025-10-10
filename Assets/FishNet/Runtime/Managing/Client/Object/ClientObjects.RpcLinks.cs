using FishNet.Managing.Logging;
using FishNet.Managing.Object;
using FishNet.Managing.Utility;
using FishNet.Object;
using FishNet.Object.Helping;
using FishNet.Serializing;
using FishNet.Transporting;
using FishNet.Utility.Extension;
using GameKit.Utilities;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace FishNet.Managing.Client
{
    /// <summary>
    /// Handles objects and information about objects for the local client. See ManagedObjects for inherited options.
    /// </summary>
    public partial class ClientObjects : ManagedObjects
    {

        #region Private.
        /// <summary>
        /// RPCLinks of currently spawned objects.
        /// </summary>
        private Dictionary<ushort, RpcLink> _rpcLinks = new Dictionary<ushort, RpcLink>();
        #endregion

        /// <summary>
        /// Parses a received RPCLink.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="index"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ParseRpcLink(PooledReader reader, ushort index, Channel channel)
        {
            int dataLength = Packets.GetPacketLength(ushort.MaxValue, reader, channel);

            //Link index isn't stored.
            if (!_rpcLinks.TryGetValueIL2CPP(index, out RpcLink link))
            {
                SkipDataLength(index, reader, dataLength);
                return;
            }
            else
            //Found NetworkObject for link.
            if (Spawned.TryGetValueIL2CPP(link.ObjectId, out NetworkObject nob))
            {
                NetworkBehaviour nb = nob.NetworkBehaviours[link.ComponentIndex];
                if (link.RpcType == RpcType.Target)
                {
                    var positionBefore = reader.Position;
                    nb.OnTargetRpc(link.RpcHash, reader, channel);
                    // rpc read length is variable; so compare the before and after buffer position to know the real size
                    OnPacketRead?.Invoke(new PacketProcessingArgs(nb, (int)link.RpcHash, PacketId.TargetRpc, reader.Position-positionBefore));
                }
                else if (link.RpcType == RpcType.Observers)
                {
                    var positionBefore = reader.Position;
                    nb.OnObserversRpc(link.RpcHash, reader, channel);
                    // rpc read length is variable; so compare the before and after buffer position to know the real size
                    OnPacketRead?.Invoke(new PacketProcessingArgs(nb, (int)link.RpcHash, PacketId.ObserversRpc, reader.Position-positionBefore));
                }
                else if (link.RpcType == RpcType.Reconcile)
                {
                    var positionBefore = reader.Position;
                    nb.OnReconcileRpc(link.RpcHash, reader, channel);
                    // rpc read length is variable; so compare the before and after buffer position to know the real size
                    OnPacketRead?.Invoke(new PacketProcessingArgs(nb, (int)link.RpcHash, PacketId.Reconcile, reader.Position-positionBefore));
                }
            }
            //Could not find NetworkObject.
            else
            {
                SkipDataLength(index, reader, dataLength, link.ObjectId);
            }
        }

        /// <summary>
        /// Sets link to rpcLinks key linkIndex.
        /// </summary>
        /// <param name="linkIndex"></param>
        /// <param name="link"></param>
        internal void SetRpcLink(ushort linkIndex, RpcLink link)
        {
            _rpcLinks[linkIndex] = link;
        }

        /// <summary>
        /// Removes link index keys from rpcLinks.
        /// </summary>
        internal void RemoveLinkIndexes(List<ushort> values)
        {
            if (values == null)
                return;

            for (int i = 0; i < values.Count; i++)
                _rpcLinks.Remove(values[i]);
        }

    }

}