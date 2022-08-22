using System.Runtime.InteropServices;
using System;
using UnityEngine;
using AOT;
using System.Collections.Generic;
using System.Linq;
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

        public delegate void NativeWebGLCallback(string json);
        public delegate void NativeWebGLByteArrayCallback(IntPtr bytesData, string json);

        #region __Internal

        [DllImport("__Internal")]
        private static extern void init();
        [DllImport("__Internal")]
        private static extern void initializeCallback(NativeWebGLCallback callback, NativeWebGLByteArrayCallback dataCallback);
        [DllImport("__Internal")]
        private static extern void setUser(string userInfo);
        [DllImport("__Internal")]
        private static extern void beginMediaStream(int video, int audio);
        [DllImport("__Internal")]
        private static extern void connectToApp(string appKey);
        [DllImport("__Internal")]
        private static extern void sendMessageInChannel(string message);
        [DllImport("__Internal")]
        private static extern void setMuteStatusVideo(int status);
        [DllImport("__Internal")]
        private static extern void setMuteStatusAudio(int status);
        [DllImport("__Internal")]
        private static extern void setAudioVolume(string userId, int volume);
        [DllImport("__Internal")]
        private static extern void joinChannel(string channelId, int isPrivate, string password);
        [DllImport("__Internal")]
        private static extern void leaveChannel();

        #endregion

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
            init();

            initializeCallback(HandleNativeWebGLCallback, HandleNativeWebGLFloatCallback);

            JoinedUsers = new List<User>();

            State = ConnectionState.Disconnected;
        }

        public void SetUser(UserInfo user)
        {
            setUser(Newtonsoft.Json.JsonConvert.SerializeObject(user));
        }

        public void BeginMediaStream(bool isVideoEnabled = true, bool isAudioEnabled = true)
        {
            if (State != ConnectionState.Connected)
                return;

            beginMediaStream(isVideoEnabled ? 1 : 0, isAudioEnabled ? 1 : 0);
        }

        public void Connect()
        {
            if (State != ConnectionState.ReadyToConnect)
                return;

            State = ConnectionState.Connecting;

            connectToApp(GeneralConfig.Config.AppKey);
        }

        public void JoinChannel(string channel = "default", bool @private = false, string password = "")
        {
            if (State != ConnectionState.Connected)
                return;

            State = ConnectionState.JoiningChannel;

            if (password == null)
                password = string.Empty;

            joinChannel(channel, @private ? 1 : 0, password == null ? string.Empty : password);
        }

        public void LeaveChannel()
        {
            if (State != ConnectionState.JoinedChannel)
                return;

            leaveChannel();
        }

        public void SendMessage(string message)
        {
            if (State != ConnectionState.JoinedChannel)
                return;

            sendMessageInChannel(message);
        }

        public void SetMuteStatusAudio(bool mute)
        {
            if (State != ConnectionState.JoinedChannel)
                return;

            setMuteStatusAudio(mute ? 0 : 1);
        }

        public void SetMuteStatusVideo(bool mute)
        {
            if (State != ConnectionState.JoinedChannel)
                return;

            setMuteStatusVideo(mute ? 0 : 1);
        }

        public void SetAudioVolume(string userId, float volume)
        {
            if (State != ConnectionState.JoinedChannel)
                return;

            setAudioVolume(userId, (int)(volume * 100));
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

        private void ConnectUser(User user)
        {
            JoinedUsers.Add(user);

            UserConnectedEvent?.Invoke(user);
            UsersUpdatedEvent?.Invoke(JoinedUsers);
        }

        private void DisconnectUser(User user)
        {
            if (user.Id == SelfUser.Id)
            {
                SelfUser = null;
            }

            user.Dispose();
            JoinedUsers.Remove(user);

            UserDisconnectedEvent?.Invoke(user);
            UsersUpdatedEvent?.Invoke(Instance.JoinedUsers);

            if (State == ConnectionState.ReadyToConnect)
                DisconnectedEvent?.Invoke();
        }

        private void MessagePosted(MessageData messageData)
        {
            User user = Instance.GetUserById(messageData.userId);
            TextMessage textMessage = new TextMessage()
            {
                message = messageData.message,
                createdAt = (new DateTime(1970, 1, 1)).AddMilliseconds(messageData.createdAt),
                user = user.UserInfo,
            };

            user.RegisterMessage(textMessage);

            Instance.TextMessageReceivedEvent?.Invoke(textMessage);
        }

        [MonoPInvokeCallback(typeof(NativeWebGLCallback))]
        public static void HandleNativeWebGLCallback(string json)
        {
            try
            {
                CallbackDataModel callbackDataModel = Newtonsoft.Json.JsonConvert.DeserializeObject<CallbackDataModel>(json);

                if (callbackDataModel.status)
                {
                    CallbackType type = (CallbackType)Enum.Parse(typeof(CallbackType), callbackDataModel.type);

                    switch (type)
                    {
                        case CallbackType.ConnectedToServer:
                            {
                                Instance.State = ConnectionState.ReadyToConnect;

                                if (GeneralConfig.Config.autoConnect)
                                {
                                    Instance.Connect();
                                }
                            }
                            break;
                        case CallbackType.MessageReceived:
                            {
                                MessageData messageData = Newtonsoft.Json.JsonConvert.DeserializeObject<MessageData>(callbackDataModel.data);

                                Instance.MessagePosted(messageData);
                            }
                            break;
                        case CallbackType.UserConnected:
                            {
                                UserConnectedData data = Newtonsoft.Json.JsonConvert.DeserializeObject<UserConnectedData>(callbackDataModel.data);

                                User user = new User(data.id, data.user);

                                if (data.main)
                                    Instance.SelfUser = user;

                                Instance.ConnectUser(user);
                            }
                            break;
                        case CallbackType.UserDisconnected:
                            {
                                UserDisconnectedData data = Newtonsoft.Json.JsonConvert.DeserializeObject<UserDisconnectedData>(callbackDataModel.data);

                                User user = Instance.GetUserById(data.id);

                                if (user.Id == Instance.SelfUser.Id)
                                {
                                    if (Instance.State == ConnectionState.JoiningChannel)
                                        Instance.ChannelLeftEvent?.Invoke();

                                    Instance.State = ConnectionState.ReadyToConnect;
                                }

                                Instance.DisconnectUser(user);
                            }
                            break;
                        case CallbackType.ConnectFailed:
                            {
                                Instance.State = ConnectionState.ReadyToConnect;
                                Instance.ConnectFailedEvent?.Invoke(callbackDataModel.data);
                            }
                            break;
                        case CallbackType.Connected:
                            {
                                Instance.State = ConnectionState.Connected;
                                Instance.ConnectedEvent?.Invoke();
                            }
                            break;
                        case CallbackType.JoinedChannel:
                            {
                                Instance.State = ConnectionState.JoinedChannel;
                                Instance.ChannelJoinedEvent?.Invoke();     
                            }
                            break;
                        case CallbackType.JoinChannelFailed:
                            {
                                Instance.State = ConnectionState.Connected;
                                Instance.ChannelJoinFailedEvent?.Invoke(callbackDataModel.data);
                            }
                            break;
                        case CallbackType.LeftChannel:
                            {
                                Instance.State = ConnectionState.Connected;
                                Instance.ChannelLeftEvent?.Invoke();
                            }
                            break;
                        case CallbackType.ChannelStateReceived:
                            {
                                ChannelStateReceivedData data = Newtonsoft.Json.JsonConvert.DeserializeObject<ChannelStateReceivedData>(callbackDataModel.data);

                                data.messages = data.messages.OrderBy(it => it.createdAt).ToArray();

                                foreach(var messageData in data.messages)
                                {
                                    Instance.MessagePosted(messageData);
                                }
                            }
                            break;
                        case CallbackType.BeginMediaStreamSuccess:
                            {
                                Instance.StreamBeganEvent?.Invoke();           
                            }
                            break;
                        case CallbackType.BeginMediaStreamFailed:
                            {
                                Instance.StreamFailedEvent?.Invoke(callbackDataModel.data);
                            }
                            break;
                        default:
                            break;
                    }
                }

                if (Logging)
                {
                    UnityEngine.Debug.Log(json);
                }
            }
            catch (Exception ex)
            {
                if (Logging)
                {
                    Debug.Log(json);
                    Debug.LogException(ex);
                }
            }
        }

        [MonoPInvokeCallback(typeof(NativeWebGLByteArrayCallback))]
        public static void HandleNativeWebGLFloatCallback(IntPtr bytesData, string json)
        {
            try
            {
                CallbackArrayDataModel callbackArrayDataModel = Newtonsoft.Json.JsonConvert.DeserializeObject<CallbackArrayDataModel>(json);

                CallbackDataType type = (CallbackDataType)Enum.Parse(typeof(CallbackDataType), callbackArrayDataModel.type);

                switch (type)
                {
                    case CallbackDataType.VideoFrameReceived:
                        {
                            User user = Instance.GetUserById(callbackArrayDataModel.userId);

                            if (user != null)
                            {
                                if (!user.VideoFrame.Initialized)
                                {
                                    user.VideoFrame.Initialize(callbackArrayDataModel.width, callbackArrayDataModel.height);
                                }

                                user.HandleVideoFrame(bytesData);
                            }
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                if (Logging)
                {
                    Debug.LogException(ex);
                }
            }
        }

        [Serializable]
        private class CallbackDataModel
        {
            public bool status;
            public string data;
            public string type;

            [UnityEngine.Scripting.Preserve]
            public CallbackDataModel()
            {
            }

            [UnityEngine.Scripting.Preserve]
            public CallbackDataModel(bool status, string data, string type)
            {
                this.status = status;
                this.data = data;
                this.type = type;
            }
        }

        [Serializable]
        private class CallbackArrayDataModel
        {
            public int length;
            public string type;
            public string userId;
            public int width;
            public int height;

            [UnityEngine.Scripting.Preserve]
            public CallbackArrayDataModel()
            {
            }

            [UnityEngine.Scripting.Preserve]
            public CallbackArrayDataModel(int length, string userId, string type, int width, int height)
            {
                this.length = length;
                this.userId = userId;
                this.type = type;
                this.width = width;
                this.height = height;
            }
        }

        [Serializable]
        private class UserDisconnectedData
        {
            public string id;

            [UnityEngine.Scripting.Preserve]
            public UserDisconnectedData()
            {
            }

            [UnityEngine.Scripting.Preserve]
            public UserDisconnectedData(string id)
            {
                this.id = id;
            }
        }

        [Serializable]
        private class UserConnectedData
        {
            public string id;
            public bool main;
            public UserInfo user;

            [UnityEngine.Scripting.Preserve]
            public UserConnectedData()
            {
            }

            [UnityEngine.Scripting.Preserve]
            public UserConnectedData(string id, bool main, UserInfo user)
            {
                this.id = id;
                this.main = main;
                this.user = user;
            }
        }

        [Serializable]
        private class ChannelStateReceivedData
        {
            public MessageData[] messages;

            [UnityEngine.Scripting.Preserve]
            public ChannelStateReceivedData()
            {
            }

            [UnityEngine.Scripting.Preserve]
            public ChannelStateReceivedData(MessageData[] messages)
            {
                this.messages = messages;
            }
        }

        [Serializable]
        private class MessageData
        {
            public string userId;
            public string message;
            public long createdAt;

            [UnityEngine.Scripting.Preserve]
            public MessageData()
            {
            }

            [UnityEngine.Scripting.Preserve]
            public MessageData(string userId, string message, long createdAt)
            {
                this.userId = userId;
                this.message = message;
                this.createdAt = createdAt;
            }
        }

        private enum CallbackType
        {
            MessageReceived,
            Connected,
            ConnectFailed,
            UserConnected,
            UserDisconnected,
            JoinedChannel,
            LeftChannel,
            JoinChannelFailed,
            ChannelStateReceived,
            BeginMediaStreamSuccess,
            BeginMediaStreamFailed,
            ConnectedToServer
        }

        private enum CallbackDataType
        {
            VideoFrameReceived
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