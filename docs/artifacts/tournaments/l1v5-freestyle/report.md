# L1 vs L5 — freestyle 规则迁移校验

status: completed

## Run

- harness: `scripts/run-tournament.mjs`（单配对，比 `run-round-robin.mjs` 简单）
- build: Release | win32
- argv: `--games 20 --red 1 --blue 5 --tc 3+2 --seed 20260821`
- 20/20 完成，0 错误，0 和棋

## Purpose

验证把引擎从 Caro 规则（16×16、恰好五连、两端被封不算赢）迁移到 freestyle（15×15、五连及以上、不看封堵）之后，难度梯子是否仍然成立。

base seed、时限、换色顺序均与上游 100 局基线一致，**唯一变量是规则语义**。

## Result

| | games | L5 wins | L5 win rate | 95% CI |
|---|---:|---:|---:|---|
| 本次（freestyle） | 20 | 18 | 90.0% | 69.9%–97.2% |
| 上游 `l1v5-100`（Caro） | 100 | 80 | 82.5% | 73.7%–88.8% |

freestyle 的区间**覆盖了上游的点估计**，因此判定梯子未受影响。

平均手数 28.0，明显短于上游（43.65）。这是规则变化的直接后果——封端不再能防守五连、六连也算赢，局面更具战术决定性，对局更早分出胜负。**对局变短没有扰动两个档位之间的分离度。**

## Determinism

L1 是深度封顶（MaxDepth 2）且单线程，给定局面下着法可复现。L5 不是：它跑 Lazy SMP + 预判 + 时间预算，`t=`/`nps=`/深度是墙钟证据而非预言机。

因此**有意义的信号是总体胜率，而非任何单局结果**。

## Caveat

20 局对点估计而言样本偏小。本结论成立的基础是**区间与 100 局基线重叠**，而不是 90% 这个数字本身。若要检出梯子的中等幅度变化，需要更大的样本量。

## Note on baselines

`docs/artifacts/csharp-port/tournament-L1L5-summary.json` 同样记录了 L1vL5（20 局，L5 约 67%），但样本量与本次相当，**不适合作为对照基线**——用它对照会读出"梯子变陡"的假象。权威基线是 `l1v5-100`（100 局）。
