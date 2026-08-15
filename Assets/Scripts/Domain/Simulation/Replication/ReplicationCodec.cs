using System;
using System.Text;

/// <summary>
/// ClientCommand / AuthorityTick 的无引擎小端编解码；首字节为协议版本。
/// 命中条目在 Key 之后带 ActionId 与毫米落点，供客机播受击 Cue。
/// </summary>
public static class ReplicationCodec
{
    const byte Version = 1;

    /// <summary>编码上行命令。</summary>
    public static byte[] WriteClientCommand(in ClientCommand command)
    {
        var writer = new Writer(64);
        writer.WriteByte(Version);
        writer.WriteInt64(command.FrameHint);
        writer.WriteInt32(command.SenderPlayerId);
        WriteInputFrame(ref writer, command.Input);
        return writer.ToArray();
    }

    /// <summary>解码上行命令；版本不匹配时抛错。</summary>
    public static ClientCommand ReadClientCommand(byte[] payload)
    {
        var reader = new Reader(payload);
        ReadVersion(ref reader);
        long frameHint = reader.ReadInt64();
        int sender = reader.ReadInt32();
        InputFrame input = ReadInputFrame(ref reader);
        return new ClientCommand(frameHint, sender, in input);
    }

    /// <summary>编码权威 Tick。</summary>
    public static byte[] WriteAuthorityTick(AuthorityTick tick)
    {
        if (tick == null)
            throw new ArgumentNullException(nameof(tick));

        var writer = new Writer(128);
        writer.WriteByte(Version);
        writer.WriteInt64(tick.AuthorityFrame);
        writer.WriteInt32(tick.Actors.Length);
        for (int i = 0; i < tick.Actors.Length; i++)
            WriteSnapshot(ref writer, tick.Actors[i]);

        writer.WriteInt32(tick.Hits.Length);
        for (int i = 0; i < tick.Hits.Length; i++)
            WriteHit(ref writer, tick.Hits[i]);

        WriteIdArray(ref writer, tick.Spawns);
        WriteIdArray(ref writer, tick.Despawns);
        return writer.ToArray();
    }

    /// <summary>解码权威 Tick；版本不匹配时抛错。</summary>
    public static AuthorityTick ReadAuthorityTick(byte[] payload)
    {
        var reader = new Reader(payload);
        ReadVersion(ref reader);
        long frame = reader.ReadInt64();
        int actorCount = reader.ReadInt32();
        var actors = new ActorReplicationSnapshot[actorCount];
        for (int i = 0; i < actorCount; i++)
            actors[i] = ReadSnapshot(ref reader);

        int hitCount = reader.ReadInt32();
        var hits = new ReplicatedHitEvent[hitCount];
        for (int i = 0; i < hitCount; i++)
            hits[i] = ReadHit(ref reader);

        SimActorId[] spawns = ReadIdArray(ref reader);
        SimActorId[] despawns = ReadIdArray(ref reader);
        return new AuthorityTick(frame, actors, hits, spawns, despawns);
    }

    static void ReadVersion(ref Reader reader)
    {
        byte version = reader.ReadByte();
        if (version != Version)
            throw new InvalidOperationException($"复制协议版本不支持：{version}。");
    }

    static void WriteInputFrame(ref Writer writer, in InputFrame input)
    {
        writer.WriteInt64(input.Frame);
        writer.WriteActorId(input.ActorId);
        writer.WriteSByte(input.MoveX);
        writer.WriteSByte(input.MoveY);
        writer.WriteUInt64(input.ButtonsPressed);
        writer.WriteUInt64(input.ButtonsHeld);
        writer.WriteUInt64(input.ButtonsReleased);
        writer.WriteUInt16(input.MoveReferenceYawQuantized);
    }

    static InputFrame ReadInputFrame(ref Reader reader)
    {
        long frame = reader.ReadInt64();
        SimActorId actorId = reader.ReadActorId();
        sbyte moveX = reader.ReadSByte();
        sbyte moveY = reader.ReadSByte();
        ulong pressed = reader.ReadUInt64();
        ulong held = reader.ReadUInt64();
        ulong released = reader.ReadUInt64();
        ushort yaw = reader.ReadUInt16();
        return new InputFrame(frame, actorId, moveX, moveY, pressed, held, released, yaw);
    }

    static void WriteSnapshot(ref Writer writer, in ActorReplicationSnapshot snapshot)
    {
        writer.WriteActorId(snapshot.ActorId);
        writer.WriteInt32(snapshot.TeamId);
        writer.WriteByte((byte)snapshot.Kind);
        writer.WriteInt32(snapshot.PosXMm);
        writer.WriteInt32(snapshot.PosZMm);
        writer.WriteInt32(snapshot.PosYMm);
        writer.WriteInt32(snapshot.FacingMilliDeg);
        writer.WriteInt32(snapshot.MoveVxMm);
        writer.WriteInt32(snapshot.MoveVzMm);
        writer.WriteByte(snapshot.LocomotionPhase);
        writer.WriteByte(snapshot.Gait);
        writer.WriteByte(snapshot.Cardinal);
        writer.WriteInt32(snapshot.ActionId);
        writer.WriteString(snapshot.GraphNodeId);
        writer.WriteInt32(snapshot.ActionFrame);
        writer.WriteInt32(snapshot.FreezeFrames);
        writer.WriteActorId(snapshot.SelectedTargetId);
        writer.WriteInt32(snapshot.HealthMilli);
        writer.WriteInt32(snapshot.FlagsPacked);
        writer.WriteByte((byte)snapshot.VitalityEdge);
        writer.WriteUInt16(snapshot.LocomotionNormalizedMilli);
    }

    static ActorReplicationSnapshot ReadSnapshot(ref Reader reader)
    {
        return new ActorReplicationSnapshot(
            reader.ReadActorId(),
            reader.ReadInt32(),
            (ReplicationActorKind)reader.ReadByte(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadByte(),
            reader.ReadByte(),
            reader.ReadByte(),
            reader.ReadInt32(),
            reader.ReadString(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadActorId(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            (VitalityReplicationEdge)reader.ReadByte(),
            reader.ReadUInt16());
    }

    static void WriteHit(ref Writer writer, in ReplicatedHitEvent hit)
    {
        writer.WriteInt64(hit.Frame);
        writer.WriteInt64(hit.Key.Frame);
        writer.WriteActorId(hit.Key.AttackerId);
        writer.WriteInt32(hit.Key.ActionInstanceId);
        writer.WriteInt32(hit.Key.HitboxIndex);
        writer.WriteActorId(hit.Key.TargetId);
        writer.WriteInt32(hit.ActionId);
        writer.WriteInt32(hit.HitXMm);
        writer.WriteInt32(hit.HitYMm);
        writer.WriteInt32(hit.HitZMm);
        writer.WriteInt32(hit.DirXMm);
        writer.WriteInt32(hit.DirZMm);
    }

    static ReplicatedHitEvent ReadHit(ref Reader reader)
    {
        long frame = reader.ReadInt64();
        var key = new SimHitKey(
            reader.ReadInt64(),
            reader.ReadActorId(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadActorId());
        return new ReplicatedHitEvent(
            frame,
            key,
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32());
    }

    static void WriteIdArray(ref Writer writer, SimActorId[] ids)
    {
        writer.WriteInt32(ids.Length);
        for (int i = 0; i < ids.Length; i++)
            writer.WriteActorId(ids[i]);
    }

    static SimActorId[] ReadIdArray(ref Reader reader)
    {
        int count = reader.ReadInt32();
        if (count == 0)
            return Array.Empty<SimActorId>();

        var ids = new SimActorId[count];
        for (int i = 0; i < count; i++)
            ids[i] = reader.ReadActorId();
        return ids;
    }

    struct Writer
    {
        byte[] _buffer;
        int _count;

        public Writer(int capacity)
        {
            _buffer = new byte[capacity];
            _count = 0;
        }

        public void WriteByte(byte value)
        {
            Ensure(1);
            _buffer[_count++] = value;
        }

        public void WriteSByte(sbyte value) => WriteByte(unchecked((byte)value));

        public void WriteUInt16(ushort value)
        {
            Ensure(2);
            _buffer[_count++] = (byte)value;
            _buffer[_count++] = (byte)(value >> 8);
        }

        public void WriteInt32(int value)
        {
            Ensure(4);
            unchecked
            {
                _buffer[_count++] = (byte)value;
                _buffer[_count++] = (byte)(value >> 8);
                _buffer[_count++] = (byte)(value >> 16);
                _buffer[_count++] = (byte)(value >> 24);
            }
        }

        public void WriteInt64(long value)
        {
            Ensure(8);
            unchecked
            {
                ulong u = (ulong)value;
                for (int i = 0; i < 8; i++)
                    _buffer[_count++] = (byte)(u >> (i * 8));
            }
        }

        public void WriteUInt64(ulong value)
        {
            Ensure(8);
            for (int i = 0; i < 8; i++)
                _buffer[_count++] = (byte)(value >> (i * 8));
        }

        public void WriteActorId(SimActorId id) => WriteInt32(id.Value);

        public void WriteString(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                WriteInt32(0);
                return;
            }

            byte[] utf8 = Encoding.UTF8.GetBytes(value);
            WriteInt32(utf8.Length);
            Ensure(utf8.Length);
            Buffer.BlockCopy(utf8, 0, _buffer, _count, utf8.Length);
            _count += utf8.Length;
        }

        public byte[] ToArray()
        {
            var result = new byte[_count];
            Buffer.BlockCopy(_buffer, 0, result, 0, _count);
            return result;
        }

        void Ensure(int additional)
        {
            int needed = _count + additional;
            if (needed <= _buffer.Length)
                return;

            int next = _buffer.Length * 2;
            if (next < needed)
                next = needed;
            var grown = new byte[next];
            Buffer.BlockCopy(_buffer, 0, grown, 0, _count);
            _buffer = grown;
        }
    }

    struct Reader
    {
        readonly byte[] _buffer;
        int _offset;

        public Reader(byte[] buffer)
        {
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            _offset = 0;
        }

        public byte ReadByte()
        {
            Ensure(1);
            return _buffer[_offset++];
        }

        public sbyte ReadSByte() => unchecked((sbyte)ReadByte());

        public ushort ReadUInt16()
        {
            Ensure(2);
            int lo = _buffer[_offset++];
            int hi = _buffer[_offset++];
            return (ushort)(lo | (hi << 8));
        }

        public int ReadInt32()
        {
            Ensure(4);
            unchecked
            {
                int value = _buffer[_offset]
                    | (_buffer[_offset + 1] << 8)
                    | (_buffer[_offset + 2] << 16)
                    | (_buffer[_offset + 3] << 24);
                _offset += 4;
                return value;
            }
        }

        public long ReadInt64()
        {
            Ensure(8);
            ulong u = 0;
            for (int i = 0; i < 8; i++)
                u |= (ulong)_buffer[_offset++] << (i * 8);
            return unchecked((long)u);
        }

        public ulong ReadUInt64()
        {
            Ensure(8);
            ulong u = 0;
            for (int i = 0; i < 8; i++)
                u |= (ulong)_buffer[_offset++] << (i * 8);
            return u;
        }

        public SimActorId ReadActorId()
        {
            int value = ReadInt32();
            return value <= 0 ? SimActorId.Invalid : new SimActorId(value);
        }

        public string ReadString()
        {
            int length = ReadInt32();
            if (length <= 0)
                return string.Empty;

            Ensure(length);
            string text = Encoding.UTF8.GetString(_buffer, _offset, length);
            _offset += length;
            return text;
        }

        void Ensure(int additional)
        {
            if (_offset + additional > _buffer.Length)
                throw new InvalidOperationException("复制载荷长度不足。");
        }
    }
}
