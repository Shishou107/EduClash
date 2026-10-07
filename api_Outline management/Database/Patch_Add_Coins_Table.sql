-- ====================================================================================================
-- FILE: Patch_Add_Coins_Table.sql
-- CHẠY LỆNH NÀY TRONG TERMINAL SQL CỦA SOMEE ĐỂ BỔ SUNG CỘT Coins VÀ BẢNG CoinTransactions
-- ====================================================================================================

-- 1. Bổ sung cột Coins vào bảng auth.Profiles nếu chưa có
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'auth.Profiles') AND name = N'Coins')
BEGIN
    ALTER TABLE auth.Profiles ADD Coins INT NOT NULL DEFAULT 50;
    PRINT N'[OK] Đã thêm cột Coins vào bảng auth.Profiles thành công.';
END
ELSE
BEGIN
    PRINT N'[OK] Cột Coins đã tồn tại trong auth.Profiles.';
END
GO

-- 2. Tạo bảng auth.CoinTransactions nếu chưa có
IF OBJECT_ID(N'auth.CoinTransactions', N'U') IS NULL
BEGIN
    CREATE TABLE auth.CoinTransactions (
        TransactionId BIGINT IDENTITY(1,1) NOT NULL,
        UserId UNIQUEIDENTIFIER NOT NULL,
        Amount INT NOT NULL,
        TransactionType NVARCHAR(50) NOT NULL,
        Description NVARCHAR(255) NOT NULL,
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT PK_CoinTransactions PRIMARY KEY CLUSTERED (TransactionId),
        CONSTRAINT FK_CoinTransactions_Users FOREIGN KEY (UserId) REFERENCES auth.Users(UserId) ON DELETE CASCADE
    );
    PRINT N'[OK] Đã tạo bảng auth.CoinTransactions thành công.';
END
ELSE
BEGIN
    PRINT N'[OK] Bảng auth.CoinTransactions đã tồn tại.';
END
GO
