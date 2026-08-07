# Design

以 `config_documents` 單列保存 `EasyLotteryConfigDocument` JSON，透過 upsert 保證更新原子性。DI 只替換 `IEasyLotteryConfigStore`，因此 Poke、Roulette、ActivityResult 等既有 Domain service 不需改動。

