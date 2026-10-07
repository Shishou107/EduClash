using System;

namespace api_Outline_management.Services;

public interface IEloService
{
    (int player1Delta, int player2Delta) CalculateEloDeltas(int player1Rating, int player2Rating, double player1ActualScore, int kFactor = 32);
}

public class EloService : IEloService
{
    public (int player1Delta, int player2Delta) CalculateEloDeltas(int player1Rating, int player2Rating, double player1ActualScore, int kFactor = 32)
    {
        // 1. Tính điểm kỳ vọng (Expected Score)
        // E_A = 1 / (1 + 10 ^ ((R_B - R_A) / 400))
        double expectedPlayer1 = 1.0 / (1.0 + Math.Pow(10.0, (player2Rating - player1Rating) / 400.0));
        double expectedPlayer2 = 1.0 - expectedPlayer1;

        // 2. Điểm thực tế của Player 2
        double player2ActualScore = 1.0 - player1ActualScore; // 1.0 = Win, 0.5 = Draw, 0.0 = Loss

        // 3. Tính toán độ biến thiên (Delta)
        int player1Delta = (int)Math.Round(kFactor * (player1ActualScore - expectedPlayer1));
        int player2Delta = (int)Math.Round(kFactor * (player2ActualScore - expectedPlayer2));

        return (player1Delta, player2Delta);
    }
}

public interface IXpLevelService
{
    int CalculateLevel(long currentXp);
    string DetermineRankTier(int rating);
}

public class XpLevelService : IXpLevelService
{
    public int CalculateLevel(long currentXp)
    {
        if (currentXp < 0) currentXp = 0;
        // Công thức đặc tả: Level = Floor(Sqrt(CurrentXP / 100)) + 1
        return (int)Math.Floor(Math.Sqrt(currentXp / 100.0)) + 1;
    }

    public string DetermineRankTier(int rating)
    {
        return rating switch
        {
            < 1000 => "Tân Binh",
            >= 1000 and <= 1499 => "Đồng",
            >= 1500 and <= 1999 => "Bạc",
            >= 2000 and <= 2499 => "Vàng",
            >= 2500 and <= 2999 => "Bạch Kim",
            _ => "Kim Cương"
        };
    }
}
