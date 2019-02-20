namespace Inew.Network
{
    // イベントの種類.
    public enum NetEventType
    {
        Connect = 0,    // 接続イベント.
        Disconnect,     // 切断イベント.
        SendError,      // 送信エラー.
        ReceiveError,   // 受信エラー.
    }

    // イベントの結果.
    public enum NetEventResult
    {
        Failure = -1,   // 失敗.
        Success = 0,    // 成功.
    }

    // イベントの状態通知.
    public class NetEventState
    {
        public NetEventType type;   // イベントタイプ.
        public NetEventResult result;   // イベントの結果.
    }
}
