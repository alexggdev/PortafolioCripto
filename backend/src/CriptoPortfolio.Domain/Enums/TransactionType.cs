namespace CriptoPortfolio.Domain.Enums;

public enum TransactionType
{
    Buy = 1,
    Sell = 2,
    TransferIn = 3,
    TransferOut = 4,
    Staking = 5,   // entra como compra a coste cero
    Airdrop = 6,   // entra como compra a coste cero
    Fee = 7
}
