public enum LobbyRequestType
{
    Unknown = 0,

    CreateRequest = 1,
    CreateSuccess = 2,
    CreateFail = 3,
    CreateTimeout = 4,

    JoinRequest = 5,
    JoinSuccess = 6,
    JoinFail = 7,
    JoinRoomClosed = 8,
    JoinRoomFull = 9,
    JoinTimeout = 10,

    LeaveRequest = 11,
    LeaveSuccess = 12,
    LeaveFail = 13,
    LeaveTimeout = 14,

    StartRequest = 15,
    StartSuccess = 16,
    StartFail = 17,
    StartTimeout = 18,

    LoginFail = 19,
    LoginSaveMismatch = 20,
}
