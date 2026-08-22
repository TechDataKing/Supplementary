using Microsoft.Data.SqlClient;
using TechKingPOS.App.Models;

namespace TechKingPOS.App.Data;

public static class BusinessRepository
{
    public static void Save(
        BusinessRegistrationResponse response,
        BusinessRegistrationModel model)
    {
        using SqlConnection con =
            DbService.GetConnection();

        con.Open();

        string sql =
        @"
INSERT INTO Business
(
    BusinessCode,
    BusinessName,
    OwnerName,
    Phone,
    Email,
    CloudEnabled
)
VALUES
(
    @BusinessCode,
    @BusinessName,
    @OwnerName,
    @Phone,
    @Email,
    1
);
";

        using SqlCommand cmd = new(sql, con);

        cmd.Parameters.AddWithValue("@BusinessCode", response.BusinessCode);
        cmd.Parameters.AddWithValue("@BusinessName", model.BusinessName);
        cmd.Parameters.AddWithValue("@OwnerName", model.OwnerName);
        cmd.Parameters.AddWithValue("@Phone", model.Phone);
        cmd.Parameters.AddWithValue("@Email", model.Email);

        cmd.ExecuteNonQuery();
    }
 
public static bool IsRegistered()
{
    using SqlConnection con = DbService.GetConnection();

    con.Open();

    using SqlCommand cmd = new(
    @"
SELECT CASE
    WHEN EXISTS
    (
        SELECT 1
        FROM Business
    )
    THEN 1
    ELSE 0
END;
", con);

    return Convert.ToBoolean(cmd.ExecuteScalar());
}

}