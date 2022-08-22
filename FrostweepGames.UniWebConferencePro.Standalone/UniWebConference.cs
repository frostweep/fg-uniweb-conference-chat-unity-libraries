using System.Runtime.InteropServices;
using System;
using UnityEngine;
using System.Collections.Generic;
using FrostweepGames.UniWebConferencePro.Common;

namespace FrostweepGames.UniWebConferencePro
{
    public class UniWebConference
    {
        public static bool Logging = false;

        public event Action ConnectedEvent;

        public event Action<string> ConnectFailedEvent;

        public event Action DisconnectedEvent;

        public event Action ChannelJoinedEvent;

        public event Action ChannelLeftEvent;

        public event Action<string> ChannelJoinFailedEvent;

        public event Action StreamBeganEvent;

        public event Action<string> StreamFailedEvent;

        public event Action<TextMessage> TextMessageReceivedEvent;

        public event Action<List<User>> UsersUpdatedEvent;

        public event Action<User> UserConnectedEvent;

        public event Action<User> UserDisconnectedEvent;

        private static UniWebConference _Instance;
        public static UniWebConference Instance
        {
            get
            {
                if (_Instance == null)
                    _Instance = new UniWebConference();

                return _Instance;
            }
        }

        public User SelfUser { get; private set; }

        public List<User> JoinedUsers { get; private set; }

        public ConnectionState State { get; private set; } = ConnectionState.Unknown;

        public UniWebConference()
        {
            JoinedUsers = new List<User>();

            State = ConnectionState.Disconnected;
        }

        public void SetUser(UserInfo user)
        {
        }

        public void BeginMediaStream(bool isVideoEnabled = true, bool isAudioEnabled = true)
        {
            if (State != ConnectionState.Connected)
                return;
        }

        public void Connect()
        {
            if (State != ConnectionState.ReadyToConnect)
                return;

            State = ConnectionState.Connecting;
        }

        public void JoinChannel(string channel = "default", bool @private = false, string password = "")
        {
            if (State != ConnectionState.Connected)
                return;

            State = ConnectionState.JoiningChannel;

            if (password == null)
                password = string.Empty;
        }

        public void LeaveChannel()
        {
            if (State != ConnectionState.JoinedChannel)
                return;
        }

        public void SendMessage(string message)
        {
            if (State != ConnectionState.JoinedChannel)
                return;
        }

        public void SetMuteStatusAudio(bool mute)
        {
            if (State != ConnectionState.JoinedChannel)
                return;
        }

        public void SetMuteStatusVideo(bool mute)
        {
            if (State != ConnectionState.JoinedChannel)
                return;
        }

        public void SetAudioVolume(string userId, float volume)
        {
            if (State != ConnectionState.JoinedChannel)
                return;
        }

        public void ProcessSpatialAudio(User target)
        {
            if (State != ConnectionState.JoinedChannel)
                return;

            if (target == null)
                return;

            if (SelfUser.Transform != null && target.Transform != null && SelfUser != target)
            {
                float volume = GetSpatialAudioVolume(SelfUser.Transform.position,
                                                     target.Transform.position,
                                                     GeneralConfig.Config.spatialAudioRadius,
                                                     GeneralConfig.Config.spatialAudioMinimalHearRadius);
                SetAudioVolume(target.Id, volume);
            }
        }

        public float GetSpatialAudioVolume(Vector3 sourcePosition, Vector3 targetPosition, float radius, float minimalRadius)
        {
            float difference = radius - Mathf.Clamp(Vector3.Distance(sourcePosition, targetPosition) - minimalRadius, 0, 99999);

            return GeneralConfig.Config.spatialAudioCurve.Evaluate(Mathf.Clamp01(difference / radius));
        }

        public User GetUserById(string id)
        {
            return JoinedUsers.Find(it => it.Id == id);
        }

        public enum ConnectionState
        {
            Unknown,

            ReadyToConnect,
            Connecting,
            Connected,
            Disconnected,
            JoiningChannel,
            JoinedChannel,
        }

        public class TextMessage
        {
            public string message;
            public DateTime createdAt;
            public UserInfo user;

            [UnityEngine.Scripting.Preserve]
            public TextMessage()
            {
            }
        }

        public class VideoFrame
        {
            public event Action FrameInitializedEvent;

            public event Action FrameUpdatedEvent;

            //private Color32[] _colors;

            //private byte[] _stream;

            private int _dataLength;

            public Texture2D Texture { get; private set; }
            public bool Initialized { get; private set; }

            [UnityEngine.Scripting.Preserve]
            public VideoFrame()
            {
            }

            public void Initialize(int width, int height/*, int length*/)
            {
                Texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
                Texture.wrapMode = TextureWrapMode.Clamp;
                //_colors = Texture.GetPixels32();
                //_stream = new byte[length];
                _dataLength = width * height * 4;

                Initialized = true;

                FrameInitializedEvent?.Invoke();
            }

            public void HandleFrame(IntPtr bytesData)
            {
                //int width = Texture.width;
                //int height = Texture.height;

                //Marshal.Copy(bytesData, _stream, 0, _dataLength);

                //int dataOffset = 0;
                //int index = 0;
                //int step = 4;
                //for (int h = height - 1; h >= 0; h--)
                //{
                //    for (int w = 0; w < width; w++)
                //    {
                //        index = w + h * height;

                //        _colors[index].r = _stream[dataOffset];
                //        _colors[index].g = _stream[dataOffset + 1];
                //        _colors[index].b = _stream[dataOffset + 2];
                //        _colors[index].a = _stream[dataOffset + 3];

                //        dataOffset += step;
                //    }
                //}

                Texture.LoadRawTextureData(bytesData, _dataLength);

                //Texture.SetPixels32(_colors);
                Texture.Apply();

                Marshal.FreeHGlobal(bytesData);

                FrameUpdatedEvent?.Invoke();
            }

            public void Dispose()
            {
                //_colors = null;
                //_stream = null;
                if (Texture != null)
                {
                    MonoBehaviour.Destroy(Texture);
                    Texture = null;
                }

                Initialized = false;

                FrameUpdatedEvent?.Invoke();
            }
        }

        public class UserInfo
        {
            public string name;

            [UnityEngine.Scripting.Preserve]
            public UserInfo()
            {
            }
        }

        public class User
        {
            public string Id { get; }
            public UserInfo UserInfo { get; private set; }
            public VideoFrame VideoFrame { get; private set; }
            public List<TextMessage> TextMessages { get; private set; }
            public Transform Transform { get; private set; }

            [UnityEngine.Scripting.Preserve]
            public User(string id, UserInfo userInfo)
            {
                Id = id;
                UserInfo = userInfo;
                VideoFrame = new VideoFrame();
                TextMessages = new List<TextMessage>();
            }

            public void Dispose()
            {
                VideoFrame.Dispose();
                TextMessages.Clear();
                VideoFrame = null;
                TextMessages = null;
                UserInfo = null;
                Transform = null;
            }

            public void HandleVideoFrame(IntPtr bytesData)
            {
                VideoFrame?.HandleFrame(bytesData);
            }

            public void RegisterMessage(TextMessage textMessage)
            {
                TextMessages?.Add(textMessage);
            }

            public void AttachOn(Transform transform)
            {
                Transform = transform;
            }
        }
    }
}