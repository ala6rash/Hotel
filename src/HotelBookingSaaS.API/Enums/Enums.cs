namespace HotelBookingSaaS.API.Enums;

public enum PmsType
{
    SkyHorizon = 1,
    OracleOpera = 2,
    Mews = 3,
    Hotelogix = 4,
    EZee = 5,
    Protel = 6,
    Manual = 99
}

public enum OTAType
{
    BookingCom = 1,
    Airbnb = 2,
    Expedia = 3,
    Agoda = 4,
    Hotels = 5,
    Direct = 6,
    Channex = 7
}

public enum BookingStatus
{
    Pending = 1,
    Confirmed = 2,
    CheckedIn = 3,
    CheckedOut = 4,
    Cancelled = 5,
    NoShow = 6
}

public enum BookingSource
{
    OTA = 1,
    PMS = 2,
    Direct = 3,
    Webhook = 4
}

public enum SyncStatus
{
    Success = 1,
    Failed = 2,
    Partial = 3,
    Running = 4
}

public enum UserRole
{
    SuperAdmin = 1,
    HotelAdmin = 2,
    Manager = 3,
    Staff = 4
}

public enum NotificationType
{
    NewBooking = 1,
    BookingCancelled = 2,
    BookingModified = 3,
    SyncFailed = 4,
    CredentialExpired = 5,
    LowAvailability = 6
}

public enum RoomType
{
    Single = 1,
    Double = 2,
    Twin = 3,
    Triple = 4,
    Suite = 5,
    Deluxe = 6,
    Family = 7,
    Studio = 8
}
