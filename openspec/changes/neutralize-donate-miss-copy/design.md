## Context

Donate OBS 共用獎項結果模板與 AI 恭喜文案流程，未依 `IsWinning` 區分銘謝惠顧與得獎結果。

## Goals / Non-Goals

**Goals:** 未中獎時提供不含恭喜語意的感謝文案，並避免產生不適用的 AI 祝賀文字。

**Non-Goals:** 不變更銘謝惠顧名稱、抽獎機率或中獎結果文案。

## Decisions

- 以抽獎記錄的 `IsWinning` 作為唯一判定依據，避免依獎項文字進行脆弱比對。
- 未中獎時直接使用固定中性文案；只有中獎結果才可呼叫 AI 文案與結果模板。

## Risks / Trade-offs

- [固定文案少於 AI 個人化] → 未中獎訊息以清楚、尊重的語意優先。
