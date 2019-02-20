using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Inew.Network
{
    public class PacketQueue
    {
        // パケット格納情報.
        struct PacketInfo
        {
            public int offset;
            public int size;
        };

        //
        private MemoryStream m_streamBuffer;
        private List<PacketInfo> m_offsetList;
        private int m_offset = 0;

        private readonly object _lockObj = new object();

        // 
        public PacketQueue()
        {
            m_streamBuffer = new MemoryStream();
            m_offsetList = new List<PacketInfo>();
        }

        // 
        public int Enqueue(byte[] data, int size)
        {
            PacketInfo info = new PacketInfo();

            info.offset = m_offset;
            info.size = size;

            lock (_lockObj)
            {
                // パケット格納情報を保存.
                m_offsetList.Add(info);

                // パケットデータを保存.
                m_streamBuffer.Position = m_offset;
                m_streamBuffer.Write(data, 0, size);
                m_streamBuffer.Flush();
                m_offset += size;
            }

            return size;
        }

        public int Dequeue(ref byte[] buffer, int size)
        {
            if (m_offsetList.Count <= 0)
            {
                return -1;
            }

            int recvSize = 0;

            lock (_lockObj)
            {
                PacketInfo info = m_offsetList[0];

                // バッファから該当するパケットデータを取得する.
                int dataSize = Math.Min(size, info.size);
                m_streamBuffer.Position = info.offset;
                recvSize = m_streamBuffer.Read(buffer, 0, dataSize);

                // キューデータを取り出したので先頭要素を削除.
                if (recvSize > 0)
                {
                    m_offsetList.RemoveAt(0);
                }

                // すべてのキューデータを取り出したときはストリームをクリアしてメモリを節約する.
                if (m_offsetList.Count == 0)
                {
                    Clear();
                    m_offset = 0;
                }
            }

            return recvSize;
        }

        public void Clear()
        {
            byte[] buffer = m_streamBuffer.GetBuffer();
            Array.Clear(buffer, 0, buffer.Length);

            m_streamBuffer.Position = 0;
            m_streamBuffer.SetLength(0);
        }
    }
}
