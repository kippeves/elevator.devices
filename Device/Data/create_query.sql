CREATE TABLE [DeviceInfo] (
    [DeviceId]         NVARCHAR (128) NOT NULL,
    [ConnectionString] NVARCHAR (MAX) NULL,
    [DeviceName]       NVARCHAR (MAX) NOT NULL,
    [DeviceType]       NVARCHAR (MAX) NOT NULL,
    [Location]         NVARCHAR (MAX) NOT NULL,
    [Owner]            NVARCHAR (MAX) NOT NULL,
    [Interval]         INT            NOT NULL,
    PRIMARY KEY CLUSTERED ([DeviceId] ASC)
);