namespace NxtHotspot.Domain.Enums;

public enum AuthenticationType
{
    MobileOTP,
    RoomMobile,
    StudentPassword,
    StudentOTP,
    StaffPassword,
    EmployeePassword,
    Voucher,
    UsernamePassword,
    GuestOTP
}

public enum OrganizationStatus
{
    Pending,
    Active,
    Suspended,
    Inactive
}

public enum WalletTransactionType
{
    Credit,
    Debit
}
