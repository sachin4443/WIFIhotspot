namespace NxtHotspot.Domain.Enums;

public enum NasVendor { MikroTik, Other }

/// <summary>Who a subscriber is. Drives the login workflow and the fields an admin fills in.</summary>
public enum SubscriberType { Guest, RoomGuest, Student, Teacher, Staff, Employee, Customer }

public enum SubscriberStatus { Active, Suspended, Expired, CheckedOut }

public enum SubscriberGroupType { Department, Class, Course, Batch, Floor, Other }

public enum RadiusSessionStatus { Active, Stopped, Stale }

public enum RadiusPacketKind { AccessRequest, AccountingRequest, DisconnectRequest, CoaRequest }

public enum RadiusResult { Accept, Reject, Dropped, Accounted, Ack, Nak, Timeout }

public enum DisconnectStatus { Pending, Sent, Acknowledged, Rejected, TimedOut, Failed }

public enum OtpPurpose { HotspotLogin }
