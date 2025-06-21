## Neural Paging: Learning Context Management Policies for Turing-Complete Agents

Liang Chen

Qi Liu

February 2026

## Abstract

The proof that Large Language Models (LLMs) augmented with external read-write memory constitute a computationally universal system has established the theoretical foundation for general-purpose agents. However, existing implementations face a critical bottleneck: the finite and costly Context Window, which functions not as infinite memory but as a scarce semantic cache. In this work, we introduce Neural Paging , a hierarchical architecture that decouples symbolic reasoning from information resource management. We formulate the Context Paging Problem (CPP) and propose a lightweight, differentiable Page Controller designed to approximate 'Semantic Belady's Optimality'-retaining tokens with high future utility under explicit assumptions on access patterns. We provide theoretical analysis showing that, under bounded context window size K , Neural Paging reduces the asymptotic complexity of long-horizon reasoning from quadratic O ( N 2 ) to O ( N · K 2 ) , and we derive a robustness bound (Theorem 4) that quantifies competitive-ratio degradation under policy-dependent access with bounded sensitivity. We validate these bounds on synthetic paging traces, confirming that the theoretical guarantees hold and identifying significant slack that motivates learned policies.

Keywords: Hierarchical Neural Turing Machine, Context Paging Problem, Semantic Cache, Competitive Analysis, Reinforcement Learning.

## 1 Introduction

Large Language Models (LLMs) have evolved from static statistical predictors to the central cognitive kernels of autonomous Agents. As these agents are deployed in complex, open-ended environments-ranging from software engineering to scientific discovery-they are required to maintain coherent reasoning over increasingly long horizons. This shift represents a fundamental transition from 'Stateless Function Approximation' to 'Stateful Turing Computation.'

However, this transition is hindered by a critical physical constraint: the Context Window . Despite recent advances in extending context lengths (e.g., to 1M+ tokens), the 'Lost in the Middle' phenomenon [17] persists, where effective reasoning capabilities degrade as salient information is buried in noise. Furthermore, the quadratic computational cost of the Transformer's self-attention mechanism makes processing massive contexts prohibitively expensive and slow for real-time applications.

Current approaches to managing this bottleneck are insufficient. Retrieval-Augmented Generation (RAG) functions as a naive, passive fetching mechanism with coarse granularity, often leading to context fragmentation. Direct Long Context extension hits memory walls and suffers from 'distraction' issues. MemGPT [22] introduces a tiered memory system but relies on the LLM itself to manage system calls ('Kernel-in-User-Space'). This design is inefficient, forcing the high-level reasoning engine to perform low-level resource management, consuming valuable tokens and attention heads for housekeeping tasks rather than problemsolving.

We propose Neural Paging , a framework inspired by the evolution of Operating Systems. Just as modern OS kernels separate user processes from the Memory Management Unit (MMU), we argue for a strict architectural decoupling in AI agents. We introduce the Hierarchical Neural Turing Machine (H-NTM) , where the LLM is dedicated solely to reasoning, while a secondary, learned Page Controller manages the context window. This controller acts as a neural MMU, aiming to predict future data requirements and evict low-utility tokens to approximate Belady-style decisions under explicit modeling assumptions.

Our contributions are as follows:

1. Theoretical framing. We formalize the Context Paging Problem (CPP) and define a semantic access model for LLM agents, including a bounded-sensitivity notion (Definition 3a) that quantifies policy-dependent access and enables robustness analysis beyond the classical exogenous model.
2. Architectural design. We design the H-NTM with a decoupled Page Controller that uses lightweight policy networks to manage memory operations (KEEP, EVICT,

PREFETCH) without interrupting the main reasoning loop.

3. Analytical results. Wederive complexity bounds (Theorem 2), adapt classical paging lower bounds under explicit assumptions (Theorem 3), and prove a new robustness bound (Theorem 4) showing that competitive guarantees degrade gracefully under bounded policy sensitivity.
4. Synthetic validation. We validate the theoretical bounds on synthetic paging traces with controlled parameters, confirming that (i) the bounds are satisfied, (ii) the cascade effect from access perturbation is empirically mild, and (iii) structured access patterns offer significant room for learned policies to outperform worst-case guarantees.

Scope and originality. The universality simulation (Theorem 1) and competitive-ratio lower bound (Theorem 3) are classical results carried over to make their conditions explicit in the LLM agent setting. The core new content is the bounded-sensitivity model (Definition 3a), the robustness bound (Theorem 4), and the synthetic validation. We emphasize that end-to-end evaluation on real LLM agents is a natural next step but is outside the scope of this paper.

## 2 Related Work

## 2.1 Memory-Augmented Language Models

The concept of augmenting neural networks with external memory dates back to the Neural Turing Machine (NTM) [11] and Differentiable Neural Computers (DNC) [12]. These early works focused on learning algorithmic manipulation of small, addressable memory matrices. Memory Networks [28] introduced key-value memory structures for QA tasks. More recent works like Transformer-XL [7] and Compressive Transformers [23] introduced segment-level recurrence and compressed memory for longer sequences. Our work scales these concepts to modern LLMs, treating the entire context window as a dynamic 'cache' for a much larger external store.

## 2.2 Context Extension Techniques

Efforts to extend the native context window include Position Interpolation [5]. Architectural innovations like Longformer [3] and BigBird [31] utilize sparse attention to reduce costs. More recently, Ring Attention [16] and FlashAttention [8] have optimized hardware utilization. Infini-attention [21] introduces a compressive memory to enable long-context

processing with bounded memory. However, these methods address capacity (how much can fit) rather than utility (what should fit). Neural Paging is orthogonal to these techniques, optimizing the content within whatever capacity is available.

## 2.3 Retrieval-Augmented Generation (RAG)

RAG[15] and RETRO [4] decouple knowledge from parameters. Self-RAG [1] adds a critique step to assess retrieval quality. While effective for QA, standard RAG lacks a global view of the 'Working Set' and cannot proactively manage context for multi-step reasoning, often leading to thrashing when the agent repeatedly retrieves and discards the same information.

## 2.4 LLM Agents and Operating System Analogies

The agentic paradigm, exemplified by ReAct [30] and Toolformer [24], emphasizes active environment interaction. MemGPT [22] explicitly draws parallel to Operating Systems, managing a 'main context' and 'external context.' AIOS [20] and OS-Copilot [29] further explore this analogy. Neural Paging advances this line of inquiry by replacing heuristic or LLM-driven memory management with a dedicated, learned policy network, optimizing the 'OS kernel' operations.

## 2.5 Learnable Cache Management

Cache replacement has a long history in systems. ARC [19] adaptively balances recency and frequency without manual tuning. Kraska et al. [14] demonstrate that learned models can replace classical index components. Recent KV cache management systems, such as adaptive compression [10] and KV cache streaming [18], optimize inference memory and latency at the KV cache level. Our work is complementary, focusing on semantic content selection rather than low-level KV reuse.

## 2.6 Paging Theory and Competitive Analysis

The theoretical foundations of paging are well-established. Belady's algorithm [2] provides the optimal offline policy. Competitive analysis of online paging was initiated by Sleator and Tarjan [27], who established the K b -competitiveness of LRU. The Working Set Model [9] provides a principled framework for understanding locality of reference. Our Semantic Paging extends these classical results to the stochastic, high-dimensional setting of LLM agents.

## 3 Theoretical Framework

## 3.1 Preliminaries and Notation

We establish a rigorous formal framework for analyzing context management in autoregressive language models augmented with external memory.

Table 1: Notation Summary

| Symbol     | Definition                                       |
|------------|--------------------------------------------------|
| Σ          | Finite vocabulary of tokens, &#124; Σ &#124; = V |
| T          | Finite time horizon of the task                  |
| K          | Context window capacity (in tokens)              |
| B          | Block size for paging operations                 |
| M          | Size of external memory (in blocks), M ≫ K/B     |
| K b        | Context capacity in blocks, K b = K/B            |
| L          | Language model L : Σ ∗ → Prob(Σ)                 |
| C t        | Context window content at time t                 |
| E t        | External memory state at time t                  |
| h t        | Hidden state of the agent at time t              |
| π          | Paging policy π : S → Prob( A )                  |
| S , A      | State and action spaces of the paging MDP        |
| V ( b, t ) | Value of block b at time t                       |
| D f        | Bound on additional faults per misprediction     |
| β          | Bounded policy sensitivity parameter             |
| ρ          | Candidate-set recall for approximate requests    |

## 3.2 The Agent as a Computational System

Definition 1 (Memory-Augmented Language Agent) . A Memory-Augmented Language Agent (MALA) is a 5-tuple M = ( L , C, E, π, ϕ ) where:

- L is an autoregressive language model with parameters θ
- C : [ T ] → Σ ≤ K maps time to context window content
- E : [ T ] → Σ ∗ maps time to external memory state
- π : S → A is the paging policy
- ϕ : Q r →B is the retrieval function mapping queries to memory blocks

The agent operates in discrete timesteps. At each step t , the agent: (1) generates the next token y t ∼ L ( · | C t ) ; (2) observes state s t = ( C t , E t , h t , y t ) ; (3) executes paging action a t ∼ π ( · | s t ) ; (4) transitions to state s t +1 .

Definition 2 (Agent Configuration) . The configuration of a MALA at time t is the tuple C t = ( q t , i t , C t , E t ) where q t ∈ Q is the finite control state, i t ∈ [ K ] is the attention position, C t is the context window content, and E t is the external memory content.

Remark (Markov State vs. Observation) . The full state ( C t , E t , h t , y t ) is Markov for the coupled LLM-retriever dynamics. However, the Page Controller often observes only a partial view (Definition 10), turning the learning problem into a POMDP. We discuss observability implications in Section 5.5.

Theorem 1 (Turing Completeness of Memory-Augmented LLMs) . Let M be a MALA with external memory size M = ω (1) that grows with the input length and retrieval satisfying Assumption 3. Then M can simulate any Turing machine TM . If the TM runs in T TM ( n ) steps using S ( n ) tape cells, the simulation requires O ( T TM ( n ) · B 2 ) total attention operations, which is O ( T TM ( n )) for constant block size B .

Proof Sketch. Weconstruct a simulation of a single-tape Turing machine TM = ( Q TM , Γ , b 0 , Σ TM , δ, q 0 , F ) . The TM tape is encoded as fixed-size blocks stored in external memory, each tagged with its address. By Assumption 3, the controller can fetch a block by address in O (1) queries. At each TM step, the controller fetches the block containing the head position, applies the transition, and writes back. Each step costs O ( B 2 ) attention over O ( B ) tokens. Correctness follows by induction on TM steps. Full details appear in Appendix A.1.

Remark. Theorem 1 is a constructive restatement of existing universality results for memoryaugmented LLMs [26]; we include it to make simulation costs and assumptions explicit in our setting.

## 3.3 Access Model and Assumptions

We formalize the semantic access process by mapping information needs to an abstract block request sequence.

Definition 3 (Requested Block) . Given context C t and finite external memory E t (with | E t | = M &lt; ∞ ), define the requested block at time t as:

<!-- formula-not-decoded -->

if max b ∈ E t PredGain( b ; C t ) &gt; τ for a fixed threshold τ &gt; 0 , and r t = ⊥ otherwise, where

<!-- formula-not-decoded -->

is the reduction in predictive entropy of the next-token random variable Y , and C ⊕ b denotes appending block b to context C .

Remark (Operational Approximation) . Computing PredGain over all b ∈ E t is intractable for large external memory. In practice, controllers approximate this by scoring a small candidate set or by using a learned proxy for predictive gain.

Definition 3b (Approximate Requested Block) . Let S t ⊆ E t be a candidate set produced by a retriever. Define ˆ r t = arg max b ∈ S t PredGain( b ; C t ) , with ˆ r t = ⊥ if the maximum is below τ . The candidate set has recall ρ if P ( r t ∈ S t ) ≥ ρ for all t .

Lemma 1b (Approximate Request Error Bound) . If P ( r t ∈ S t ) ≥ ρ for all t , then the expected difference in page-fault counts between using ˆ r t and r t is at most (1 -ρ ) T .

̸

Proof. A mismatch ˆ r t = r t can only occur when r t / ∈ S t , which happens with probability at most 1 -ρ at each step. Each mismatch changes the fault indicator by at most one.

Assumption 1 (Policy-Independent Access) . For competitive-ratio analysis, we assume the request sequence ( r 1 , . . . , r T ) is exogenous and independent of the eviction policy.

Assumption 2 (Retrieval Oracle) . When the controller issues PREFETCH ( q ) , the retriever returns the correct block with probability 1.

Assumption 3 (Addressable Retrieval for Simulation) . For the universality result (Theorem 1), there exists an encoding such that the retrieval function can fetch the block containing a specified address in O (1) queries.

## 3.3.1 Assumption Robustness and Relaxations

Policy-Independent Access (Assumption 1). In real LLM agents, the access sequence depends on context content, which depends on the eviction policy. This feedback can be weak (e.g., when retrieval targets are dictated by external inputs) or strong (e.g., when the agent's generation changes future needs). We formalize a relaxation:

Definition 3a (Bounded Policy Sensitivity) . Let r π denote the request sequence induced by policy π over horizon T . A task has β -bounded sensitivity if for any two policies π, π ′ , the Hamming distance satisfies d H ( r π , r π ′ ) ≤ βT .

Lemma 1a (Fault Sensitivity under Access Perturbation) . For any online paging algorithm A with cache size K b operating on sequences r, r ′ with d H ( r, r ′ ) = d :

<!-- formula-not-decoded -->

Proof. We couple the executions of A on r and r ′ . At each of the d positions where requests differ, the fault indicators differ by at most 1, contributing at most d direct faults. Additionally, the differing request can alter the cache state: the two caches may now hold different blocks, causing a cascade of at most K b additional fault mismatches at subsequent steps (until the divergent blocks are naturally evicted). Each cascade terminates within K b steps because, after K b distinct requests, the cache is fully refreshed. The cascades from different perturbations are not independent, but the total cascade length is bounded by K b · d , giving total | F A ( r ) -F A ( r ′ ) | ≤ d + K b d = ( K b +1) d . Assumption 1 corresponds to β = 0 .

Remark (Tightness) . The factor K b + 1 is a worst-case bound; synthetic experiments in Section 6 show that on Zipf-distributed traces the empirical factor is approximately 1 . 1 -1 . 2 , much smaller than K b +1 . We conjecture that a tighter bound of O (log K b ) · d may hold for traces with locality, but leave this to future work.

Retrieval Oracle (Assumption 2). If retrieval succeeds with probability ρ per request (independent errors), the expected additional faults are at most (1 -ρ ) T .

Addressable Retrieval (Assumption 3). For the universality proof, this is realized by storing an address token in each block and using exact-key retrieval in a hash-indexed store. This is a different retrieval regime from approximate semantic search.

## 3.3.2 Estimating β in Practice

The sensitivity parameter β depends on the task structure. We provide a practical estimation protocol and worked examples.

Protocol. Given a task instance, run two different baseline policies (e.g., LRU and FIFO) on the same task and measure the Hamming distance of the resulting access traces:

<!-- formula-not-decoded -->

This provides an empirical lower bound on the task's sensitivity. A more robust estimate uses ˆ β = max π,π ′ ∈ Π baseline d H ( r π , r π ′ ) /T over a set of baselines.

Worked examples (qualitative estimates based on task structure):

| Task Class                 | β (est.)      | Rationale                                                                                         |
|----------------------------|---------------|---------------------------------------------------------------------------------------------------|
| Multi-step math            | ≤ 0 . 05      | Reasoning steps determined by problem; context choice rarely changes the derivation path          |
| Code generation with tools | ≤ 0 . 10      | Tool outputs are mostly deter- mined by the code, though con- text affects which tools are called |
| Open-ended dialogue        | 0 . 2 - 0 . 5 | Each context choice substantially changes the generated response and subsequent needs             |

These estimates suggest that β is small for structured, goal-directed tasks-precisely the setting where long-horizon agents operate and where Neural Paging is most applicable.

## 3.4 The Context Paging Problem

Definition 4 (Context Block) . A context block b ∈ B is a contiguous subsequence of tokens b = ( w 1 , . . . , w B ) of fixed length B . The context window C t is a multiset of blocks with | C t | ≤ K b .

̸

Definition 5 (Semantic Page Fault) . A semantic page fault occurs at time t if r t = ⊥ and r t / ∈ C t .

Definition 6 (Block Utility Function) . The utility of block b at time t with respect to future horizon H is:

<!-- formula-not-decoded -->

where γ ∈ (0 , 1) is a discount factor.

Remark (Connection to Semantic Value Function) . Definition 6 is an oracle quantity depending on future contexts C t + k . In practice, the Semantic Value Function (Definition 12) approximates it via a learned value estimate. When γ → 0 , U ( b, t ) → PredGain( b ; C t \ b ) = R utility ( b, s t ) , so Definition 12 can be viewed as a one-step approximation of Definition 6. Multi-step rollouts or bootstrapped value estimates provide intermediate approximations.

Definition 7 (Context Paging Problem) . Given a task horizon T , context capacity K , and utility function U , find the paging policy π ∗ that maximizes:

<!-- formula-not-decoded -->

where

<!-- formula-not-decoded -->

## 3.5 Semantic Belady's Algorithm

Definition 8 (Semantic Belady's Algorithm) . The Semantic Belady algorithm is an offline policy that, upon eviction, selects:

<!-- formula-not-decoded -->

where NextUse( b, t ) = min { k ≥ 1 : r t + k = b } ( ∞ if never accessed again).

Proposition 2 (Optimality of Semantic Belady under Fixed Access) . For any fixed request sequence ( r 1 , . . . , r T ) independent of the eviction policy, Semantic Belady's algorithm minimizes the total number of page faults.

Proof. Under the assumption that the access sequence is fixed and policy-independent, this follows directly from the classic proof by Belady [2].

Caveat. In the LLM setting, the access sequence is typically not fixed-it depends on the context content. This creates a feedback loop that invalidates direct application. Proposition 2 applies only when future accesses are policy-independent (Assumption 1). For the relaxed setting, see Theorem 4.

## 3.6 Complexity Analysis

Theorem 2 (Inference Complexity) . Let M be a MALA processing a task of length N tokens with context window K , block size B , and K b = K/B . Assume ANN retrieval with O (log M ) query complexity. The total computational complexity is:

<!-- formula-not-decoded -->

When K = O (1) , this reduces to O ( N ) compared to O ( N 2 ) for full-context attention.

Proof. At each of N steps, self-attention over K tokens costs O ( K 2 ) . Paging decisions occur every B tokens; each evaluates K b blocks. Retrieval via ANN costs O (log M ) per query.

Policy inference with a two-layer network costs O ( K 2 b ) per decision. Summing gives the stated bound.

Remark. This asymptotic reduction holds for any method that fixes the context window to size K , including naive sliding windows. The distinguishing question for Neural Paging is whether it can preserve task quality under fixed K through better content selection-an empirical question.

Corollary 1 (Thrashing Condition) . A MALA with context capacity K b and working set size W experiences thrashing when K b &lt; W , where W is the minimum set of blocks that must be simultaneously resident for the task to proceed without repeated faults. This adapts the Working Set Model of Denning [9].

## 4 Methodology: The H-NTM Architecture

## 4.1 Hierarchical Neural Turing Machine

We present the H-NTM, an architecture that strictly separates reasoning (LLM) from memory management (Page Controller), analogous to the CPU-MMU separation in modern operating systems.

Definition 9 (H-NTM Architecture) . The H-NTM is defined as H = ( L , P , R , M ) where L is the Main Language Model (frozen during controller training), P is the Page Controller with parameters θ P , R is the Retriever ( R : Σ ∗ → R d ), and M is the External Memory.

The key principle is information hiding : the Main LLM operates as if it had a fixed-size context window, while the Page Controller transparently ensures that relevant information is available.

## 4.2 Interface Design

Definition 10 (Controller Interface) . The Page Controller's observation at time t is o t = Encoder( f mode ( L , C t , y &lt;t )) where f mode depends on the operational mode:

| Mode      | Observable Information                    | Applicability        |
|-----------|-------------------------------------------|----------------------|
| White-Box | Attention weights, hidden states, context | Open-source LLMs     |
| Black-Box | Output tokens, context, logits            | API-based LLMs       |
| Gray-Box  | Embeddings and outputs                    | Partially observable |

Figure 1: Hierarchical Neural Turing Machine (H-NTM) System Architecture Diagram

<!-- image -->

Figure 1: H-NTM System Architecture (schematic). The Main Agent (LLM) focuses on token generation. The Page Controller monitors activations and manages data flow between Context Window (Cache) and External Memory (Disk).

Lemma 2 (Black-Box Sufficiency under Explicit Plans) . If there exists a mapping g from output prefixes to optimal prefetch targets such that P ( g ( y &lt;t ) = b ∗ prefetch ) ≥ 1 -ϵ for all t , then a Black-Box controller implementing g achieves expected page-fault rate within ϵ of the White-Box controller.

Proof. When g is correct (probability ≥ 1 -ϵ ), the paging action matches the White-Box decision. Each incorrect prediction contributes at most one additional fault. Taking expectation yields an additive ϵ bound.

Remark (Existence of g ) . Lemma 2 is conditional. A mapping g is plausible only when output prefixes contain explicit plan tokens or tool directives with stable identifiers. Agent

frameworks like ReAct [30], which produce structured action tokens (e.g., Search[query] ), are natural candidates. For unconstrained generation, such a mapping is unlikely to exist.

## 4.3 Page Controller Architecture

Definition 11 (Page Controller Network) . The Page Controller P θ : S → Prob( A | C t | ) parameterized by θ , with mode-specific input encoding:

White-Box: P WB θ ( s t ) = Softmax( W out · GELU( W 2 · LN( W 1 · [Φ C ( C t ); Φ h ( h t ); Φ A ( A t )]))) Black-Box: P BB θ ( s t ) = Softmax( W out · GELU( W 2 · LN( W 1 · [Φ C ( C t ); Φ y ( y &lt;t ); Φ logits (logits( y t ))]))) where Φ C is a mean-pooled block encoder, Φ h a projected hidden state, Φ A an attention pattern summary, Φ y an output sequence encoder, and Φ logits a logit distribution encoder.

The action space for each block b ∈ C t is A = { KEEP , EVICT , PREFETCH( q ) } where q ∈ Q r .

## 4.4 Semantic Value Estimation

Definition 12 (Semantic Value Function) . The value of block b in state s t is:

<!-- formula-not-decoded -->

where R utility ( b, s ) = PredGain( b ; C \ b ) .

The eviction policy selects b evict = arg min b ∈ C t V θ ( b, s t ) .

## 4.5 Predictive Prefetching via CoT Analysis

Definition 13 (Intent Extraction) . Given partial output y &lt;t , the Intent Extractor produces I t = Extractor( y &lt;t ) = ( e next , e entities , e tools ) , and the prefetch query is q t = W q [ e next ; e entities ; e tools ] .

## 4.6 Training Objective and Algorithm

Definition 14 (Paging CMDP) . The Context Paging CMDP is ( S , A , P, R, g, γ ) where:

- State S = { ( C, E, h, y ) : | C | ≤ K max }
- Action A = A evict ×A fetch (per-block decisions × global prefetch)
- Transition P : S × A → ∆( S ) (determined by frozen LLM and retriever)

Figure 2: Context as Cache Hierarchy (schematic). The Context Window acts as L1/L2 Cache, requiring distinct management strategies from the massive External Knowledge Base.

<!-- image -->

- Reward R ( s, a ) = α log P ( y ∗ | C t ) -λ evict n evict ( a ) -λ fetch n fetch ( a )
- Constraint g ( s, a ) = | C t | -K max ≤ 0

Remark (Constraint Handling) . The constraint can be enforced by action masking or via Lagrangian relaxation: max π min µ ≥ 0 E [ R ( s, a )] -µ E [ g ( s, a )] .

Training regime. The Page Controller has discrete, finite actions (KEEP, EVICT, PREFETCH candidates), a state space bounded by K , and a reward signal at each paging decision. These properties make the problem more amenable to RL than typical continuous control. However, general convergence guarantees for neural network policies are unavailable due to non-convexity; Schulman et al. [25] showed that PPO provides a lower bound on

```
Algorithm 1 Neural Paging Training (PPO) 1: Initialize controller parameters θ , critic V θ 2: for epoch = 1 to N epochs do 3: Roll out π θ in LLM+retriever environment; collect τ = { ( s t , a t , r t ) } T t =1 4: Compute GAE advantages ˆ A t with λ GAE = 0 . 95 and value targets ˆ V t 5: for mini-batch B ⊂ τ , N inner epochs do 6: Update θ by maximizing the clipped PPO objective: 7: L ( θ ) = E t [min( r t ( θ ) ˆ A t , clip( r t ( θ ) , 1 -ϵ, 1+ ϵ ) ˆ A t )] -c v ( V θ ( s t ) -ˆ V t ) 2 + c e H ( π θ ) 8: end for 9: end for
```

policy improvement at each step under the clipped surrogate, but formal convergence to a global optimum remains open.

## 4.7 Handling Open-Ended Tasks

Definition 15 (Uncertainty-Based Reward) . For open-ended generation, the prediction reward is:

<!-- formula-not-decoded -->

Remark (Reward Exploitation) . The uncertainty-based reward may be exploited by policies that always prefetch 'common' information. To mitigate this, we add a novelty-weighted exploration bonus:

<!-- formula-not-decoded -->

where N fetch ( b ) counts prior fetches of block b and η &gt; 0 controls exploration strength. This encourages the controller to prefetch diverse, task-relevant blocks rather than repeatedly fetching familiar ones.

## 5 Theoretical Analysis

## 5.1 Competitive Ratio Analysis

Theorem 3 (Competitive Ratio Lower Bound, Classical Model) . Under Assumption 1, no deterministic online paging algorithm can achieve a competitive ratio better than K b against the optimal offline policy.

Proof. The adversary constructs a request sequence over K b +1 distinct blocks, cycling to force any online algorithm to repeatedly evict the block that will be requested next, while

Figure 3: Neural Paging Workflow (schematic). As reasoning progresses from t to t +2 , old blocks are evicted and new blocks are prefetched, maintaining a dynamic window of semantic relevance.

<!-- image -->

the optimal offline algorithm retains the correct K b blocks. This is the classical paging lower bound [27]; under Assumption 1, the induced block accesses form an exogenous stream and the bound applies unchanged.

Theorem 4 (Competitive Ratio under Bounded Sensitivity) . Suppose access sequences satisfy β -bounded sensitivity (Definition 3a). If an online paging algorithm A is c -competitive on exogenous sequences and the offline optimum also satisfies the sensitivity bound, then for any policy-induced sequence r π :

<!-- formula-not-decoded -->

Proof. Fix a reference policy π 0 inducing sequence r 0 := r π 0 . By Definition 3a, d H ( r π , r 0 ) ≤ βT . By Lemma 1a:

<!-- formula-not-decoded -->

<!-- formula-not-decoded -->

Since A is c -competitive on the fixed sequence r 0 : F A ( r 0 ) ≤ c F opt ( r 0 ) . Chaining:

<!-- formula-not-decoded -->

Remark. When β = 0 (Assumption 1), Theorem 4 recovers the classical c -competitive bound. The additive term ( c +1)( K b +1) βT quantifies the 'price of non-exogeneity.' For LRU ( c = K b ), the bound becomes K b · F opt + ( K b +1) 2 βT . On structured traces where β ≪ 1 (see Section 3.3.2), the additive penalty is small relative to T .

Proposition 6 (Bound under Bounded Error Impact) . If the Page Controller predicts the optimal eviction target with accuracy p and each incorrect eviction increases faults by at most D f , then:

<!-- formula-not-decoded -->

Proof. At each of T eviction points, a correct prediction (probability p ) matches Belady; an incorrect one (probability 1 -p ) adds at most D f faults. Linearity of expectation gives the bound. The bounded-error and independence assumptions are strong; correlated errors and cascade effects (cf. Lemma 1a) may amplify D f in practice.

## 5.2 Policy Comparison (Qualitative)

Definition 16 (Policy Classes) . We define: Π H = { LRU , LFU , FIFO , Random } (heuristic), Π S = { Fixed } (static), Π L = { Neural Paging } (learned).

Under Assumption 1, if a learned policy matches Belady's eviction decision on a larger fraction of steps than a heuristic, its expected fault count is lower by Proposition 6. Determining whether this holds is an empirical question. The synthetic experiments in Section 6 show that on structured traces, the gap between heuristics and Belady is substantial (competitive ratio ≈ 1 . 9 vs. worst-case bound K b = 8 ), indicating significant room for learned policies.

## 5.3 Complexity-Theoretic Considerations

Definition 17 (Finite-Horizon Optimal Paging Problem) . Given a finite MDP ( S , A , P, R, γ ) with horizon H , compute the optimal first action a ∗ 0 maximizing E [ ∑ H -1 k =0 γ k R ( s k , a k )] .

Proposition 4 (PSPACE-Membership) . The Finite-Horizon Optimal Paging Problem is in PSPACE when |S| , |A| , and H are polynomial in the input size.

Proof Sketch. Backward induction computes V t ( s ) = max a E [ R ( s, a ) + γV t +1 ( s ′ )] for all s , using O ( |S| · |A| ) space per layer and overwriting previous layers. PSPACE-hardness for general transitions remains open.

## 5.4 Expressiveness of the Policy Class

Proposition 5 (Approximation of Smooth Policies) . For any Lipschitz-continuous policy π ∗ : S → ∆( A ) and any ϵ &gt; 0 , there exists a neural network P θ with sup s ∈S ′ ∥P θ ( s ) -π ∗ ( s ) ∥ 1 &lt; ϵ on any compact S ′ ⊆ S .

Proof. By the Universal Approximation Theorem [6, 13]. However, optimal paging policies for discrete decisions may have sharp boundaries that violate Lipschitz continuity. The approximation applies only to the restricted class of smooth policies; in practice, neural policies learn smooth surrogates that may incur non-negligible ϵ .

## 5.5 Observability and POMDP Structure

The MDP formulation in Definition 14 assumes the controller observes the full state s t = ( C t , E t , h t , y t ) . In practice, the observability depends on the interface mode (Definition 10):

White-Box. The controller accesses attention weights and hidden states, which are functions of C t and the model's internal computation. Under mild assumptions (e.g., that attention patterns summarize the information needed for paging decisions), the Markov property approximately holds.

Black-Box. The controller observes only ( y &lt;t , C t , logits( y t )) . The hidden state h t -which encodes the model's internal reasoning and may predict future information needs-is unobserved. This renders the problem a POMDP. Consequently, theoretical results that rely on the full state (Proposition 4, the CMDP formulation) apply to the White-Box setting but may not transfer directly to Black-Box. In the Black-Box setting, the controller must maintain a belief state or use history-dependent policies (e.g., recurrent architectures) to compensate.

Practical impact. The gap between White-Box and Black-Box depends on how much information about future needs is encoded in hidden states vs. externalized in output tokens. For agents using structured reasoning formats (e.g., ReAct [30] with explicit tool-call tokens), the gap may be small; for free-form generation, it can be significant. Lemma 2 formalizes the condition under which the gap vanishes.

## 5.6 Summary of Theoretical Results

Table 2: Summary of Theoretical Results

| Result                         | Statement                                                                                                                                                               | Status                                                                                                                                   |
|--------------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------|------------------------------------------------------------------------------------------------------------------------------------------|
| Thm 1 Thm 2 Thm 3 Thm 4 Prop 5 | Turing completeness with O ( T TM ) simulation Complexity O ( N · K 2 ) Competitive ratio ≥ K b Robustness under β -sensitivity Neural approximation of smooth policies | Classical; explicit conditions Holds for any fixed- K method Classical lower bound New; validated synthetically Standard UAT application |

## 6 Synthetic Validation

We validate the theoretical bounds on synthetic paging traces with controlled parameters. The goals are to: (i) confirm that Theorem 4 holds empirically, (ii) characterize the tightness of the bounds, and (iii) quantify the gap between heuristic policies and the offline optimum on structured traces.

## 6.1 Experimental Setup

Trace generation. We generate access traces from a non-stationary Zipf distribution over M = 64 blocks with exponent α = 1 . 2 and a working set of size 16 that shifts every 500 steps. This models the typical access pattern of a long-horizon agent: a 'hot' set of frequently accessed blocks with periodic phase transitions. Each trace has length T = 5 , 000 .

Controlled sensitivity. To test Theorem 4, we create pairs of traces with controlled Hamming distance. Given a base trace r 0 , we produce a perturbed trace r β by flipping exactly ⌊ βT ⌋ randomly chosen positions to uniformly random blocks, yielding d H ( r 0 , r β ) = ⌊ βT ⌋ . We vary β ∈ { 0 , 0 . 02 , 0 . 05 , 0 . 1 , 0 . 15 , 0 . 2 , 0 . 3 , 0 . 4 , 0 . 5 } .

Algorithms. We compare five paging algorithms: Belady (optimal offline), LRU, LFU, FIFO, and Random eviction. All operate with cache size K b ∈ { 2 , 4 , 6 , 8 , 10 , 12 , 16 } .

Figure 4: (a) Fault rate vs. cache size on Zipf traces ( M =64 , T =5 , 000 ). Belady is optimal; LRU is the best online heuristic. LFU suffers from frequency poisoning on shifting working sets. (b) Empirical competitive ratio vs. worst-case bound K b (Theorem 3). On structured traces, online algorithms perform far better than worst-case, with LRU at ≈ 1 . 9 × optimal. Error bars: ± 1 s.d. over 10 seeds.

<!-- image -->

Metrics. Fault rate (faults /T ), competitive ratio ( F A /F opt ), and fault stability ( | F A ( r β ) -F A ( r 0 ) | ). All results are averaged over 10 random seeds with standard deviations reported.

## 6.2 Fault Rate Scaling (Figure 4)

Figure 4(a) shows fault rates as a function of cache size. Key observations:

- Belady achieves the lowest fault rate at all cache sizes, as expected. At K b = 8 , its fault rate is 0 . 121 ± 0 . 003 .
- LRU is the best online heuristic ( 0 . 226 ± 0 . 007 at K b = 8 ), followed by FIFO ( 0 . 276 ) and Random ( 0 . 280 ). LFU performs poorly ( 0 . 577 ) because it retains historically frequent blocks from past working sets rather than adapting to shifts.
- The gap between Belady and LRU narrows as K b increases, consistent with the working set model: when the cache can hold the entire working set, all reasonable policies converge.

Figure 4(b) shows empirical competitive ratios. LRU achieves a ratio of 1 . 86 ± 0 . 04 at K b = 8 -far below the worst-case bound of K b = 8 from Theorem 3. This gap between empirical and worst-case performance demonstrates that structured access patterns (Zipf with locality) are much more benign than adversarial sequences, and motivates learning policies that exploit this structure.

Figure 5: Theorem 4 validation ( K b =8 , T =5 , 000 ). (a) Fault stability: empirical | F A ( r β ) -F A ( r 0 ) | vs. reference line βT . The empirical values slightly exceed βT at small β (cascade effect), but remain well within the corrected ( K b +1) βT bound. (b) Theorem 4 bound (red dashed) vs. actual LRU faults (orange). The bound holds with large slack, indicating room for tighter instance-dependent analysis. Error bars: ± 1 s.d. over 10 seeds.

<!-- image -->

## 6.3 Theorem 4 Validation (Figure 5)

Figure 5(a) validates the fault-sensitivity bound from Lemma 1a. The empirical fault difference | F LRU ( r β ) -F LRU ( r 0 ) | grows linearly with β , closely tracking the reference line βT . At small β ( ≤ 0 . 15 ), the empirical values slightly exceed βT (by a factor of ≈ 1 . 13 ), confirming that the cascade effect described in Lemma 1a is real but mild. The corrected bound ( K b +1) βT = 9 βT holds with large margin at all tested values.

Figure 5(b) validates Theorem 4. The Theorem 4 upper bound c · F opt +( c +1)( K b +1) βT (computed with c = K b = 8 ) is satisfied at all β values, with substantial slack. This slack arises from two sources: (i) the empirical competitive ratio is ≈ 1 . 9 vs. worst-case c = 8 , and (ii) the cascade factor is ≈ 1 . 13 vs. worst-case K b +1 = 9 . Both sources of conservatism can be reduced by deriving instance-dependent bounds-an avenue for future work.

## 6.4 Summary of Experimental Findings

1. Bounds are correct: Theorem 4's bound is satisfied for all tested β values.
2. Cascade is mild: The empirical cascade factor ( ≈ 1 . 13 ) is far below the worst-case K b +1 = 9 , suggesting that the ( K b +1) factor in Lemma 1a is pessimistic for structured traces.
3. Large gap between heuristics and optimum: LRU's competitive ratio of ≈ 1 . 9 (vs. worst-case 8) shows that structured access patterns offer significant room for

improvement-the core motivation for learning paging policies.

4. LFU is fragile: On non-stationary traces, LFU can be much worse than LRU (competitive ratio ≈ 4 . 8 vs. ≈ 1 . 9 ), highlighting the danger of using heuristics mismatched to the access pattern. This further motivates adaptive, learned policies.

## 7 Discussion

## 7.1 Scope and Applicability

Neural Paging is designed for stateful, long-horizon agent tasks where information needs evolve over time:

| Scenario             | Suitability   | Reason                                         |
|----------------------|---------------|------------------------------------------------|
| Multi-step reasoning | High High Low | Working set changes predictably ( β ≤ 0 . 05 ) |
| Streaming data       |               | Continuous context turnover                    |
| Interactive dialogue | Medium        | Context grows but rarely shrinks               |
| Single-pass QA       |               | No eviction needed                             |

## 7.2 Limitations

No end-to-end evaluation. The synthetic experiments validate theoretical bounds on abstract paging traces. They do not demonstrate that Neural Paging improves task accuracy when integrated with a real LLM. A minimal validation path is: (1) synthetic traces (this paper), (2) retrieval noise sweeps with controlled ρ , (3) end-to-end agent tasks with frozen LLM measuring token cost, latency, and quality. Steps 2-3 are essential next steps.

Training complexity. The Page Controller requires task-specific training. Transfer across domains remains an open problem.

Prediction uncertainty. Future information needs are inherently uncertain; incorrect value estimates may cause suboptimal evictions.

Cold start. At episode start, the controller has limited information, causing early page faults before adaptation.

Retrieval dependencies. Neural Paging assumes reliable retrieval; if ϕ fails, faults cannot be resolved.

Theoretical conservatism. The ( K b +1) factor in Lemma 1a and the worst-case competitive ratio c = K b in Theorem 4 make the bounds conservative. Instance-dependent analysis (e.g., using the actual competitive ratio on specific trace distributions) could yield much tighter guarantees.

## 7.3 Connections to Computability Theory

The H-NTM maps cleanly onto the components of a Turing machine: the LLM corresponds to finite control, the Context Window to the tape segment under the head, and External Memory to the full tape. The Page Controller implements 'tape head movement' by managing which tape cells are in context. Neural Paging can thus be viewed as resource-bounded computation where the bound is the context window size K rather than time or space.

## 7.4 Multi-Agent Context Partitioning (Sketch)

Definition 18 (Multi-Agent H-NTM) . A multi-agent system {H i } n i =1 with shared external memory E shared . Each agent i has context C i t and policy π i , with ∑ n i =1 | C i t | ≤ K shared . The joint policy may be decentralized or coordinated.

This setting raises questions about cache contention, dynamic capacity allocation, and cooperative objectives. We include it as a concrete extension but do not pursue formal results.

## 7.5 Open Problems

1. Instance-dependent competitive bounds : Derive tighter bounds for specific trace distributions (e.g., Zipf), improving on the worst-case K b .
2. Tight cascade analysis : Prove that the empirical cascade factor O (1) (rather than K b +1 ) holds for traces with locality, and characterize the locality conditions formally.
3. Hierarchical paging : Extend to multi-level caches (L1/L2/L3 analog).
4. Adaptive block sizes : Allow variable block sizes based on semantic coherence.
5. Joint optimization : End-to-end training of LLM and Page Controller with architectural modifications.
6. Predictable access and improved guarantees : Characterize tasks where future access is predictable from context and derive competitive ratios below K b .

## 8 Conclusion

We presented the Hierarchical Neural Turing Machine and the Neural Paging framework as a principled approach to context management for long-horizon AI agents. The primary

contributions are: (1) the formal Context Paging Problem with a bounded-sensitivity access model; (2) the H-NTM architecture that decouples reasoning from memory management; (3) Theorem 4, a new robustness bound quantifying competitive-ratio degradation under policy-dependent access; and (4) synthetic experiments confirming the bounds and revealing substantial slack that motivates learned paging policies.

Key findings from the synthetic validation-that structured access patterns yield empirical competitive ratios far below worst-case bounds, and that the cascade effect from access perturbation is mild-together provide both the theoretical justification and the empirical motivation for deploying learned context management in real agent systems. End-to-end evaluation with frozen LLMs on long-horizon tasks is the natural and essential next step.

## A Proof Details

## A.1 Proof of Theorem 1

We sketch a concrete simulation. Represent the TM tape as fixed-size blocks of B tokens stored in external memory, each tagged with its address. By Assumption 3, the controller can fetch any block by address in O (1) queries.

At each TM step, the controller fetches the block containing the head position (and one adjacent block if the head crosses a boundary), applies the transition, and writes back. The context window holds O ( B ) tokens. Each step costs O ( B 2 ) attention and O (1) retrieval. Over T TM ( n ) steps: O ( T TM ( n ) · B 2 ) = O ( T TM ( n )) for constant B .

## A.2 Proof of Theorem 2

At each of N generation steps, self-attention costs O ( K 2 ) , yielding O ( NK 2 ) . Paging decisions occur every B tokens; retrieval via ANN costs O (log M ) per query for K b candidates, totaling O ( NK b log M/B ) . Policy inference costs O ( K 2 b ) per decision, totaling O ( NK 2 b /B ) .

## A.3 Proof of Theorem 3

Under Assumption 1, the classical adversarial construction over K b +1 pages applies unchanged [27].

## A.4 Proof of Theorem 4

Full proof in Section 5. The key steps: (1) apply Lemma 1a to both A and OPT with sensitivity β ; (2) use the c -competitive guarantee on the fixed reference sequence; (3) chain

the inequalities.

## A.5 Proof of Proposition 4

Backward induction: V t ( s ) = max a E [ R ( s, a ) + γV t +1 ( s ′ )] . Working space per layer: O ( |S| · |A| ) . Layers can be overwritten. Total space: O ( |S| · |A| ) .

## A.6 Proof of Proposition 6

At each of T eviction points, correct prediction (prob p ) matches Belady; error (prob 1 -p ) adds ≤ D f faults. Linearity of expectation: E [ F learned ] ≤ F opt +(1 -p ) D f T .

## B Experimental Details

Trace generation. Zipf distribution with exponent α = 1 . 2 , working set size 16, shift interval 500, total blocks M = 64 , trace length T = 5 , 000 . Ten random seeds (42-51) for all experiments.

Perturbation. For each β , exactly ⌊ βT ⌋ positions are selected uniformly at random and replaced with uniformly random blocks (distinct from the original).

Algorithms. All algorithms implemented in Python with O ( T log T ) Belady using precomputed next-use indices. Full source code: synthetic\_validation.py .

## References

- [1] Akari Asai, Zeqiu Wu, Yizhong Wang, Avirup Sil, and Hannaneh Hajishirzi. SelfRAG: Learning to retrieve, generate, and critique through self-reflection. arXiv preprint arXiv:2310.11511 , 2023.
- [2] László A Belady. A study of replacement algorithms for a virtual-storage computer. IBM Systems Journal , 5(2):78-101, 1966.
- [3] Iz Beltagy, Matthew E Peters, and Arman Cohan. Longformer: The long-document transformer. arXiv preprint arXiv:2004.05150 , 2020.
- [4] Sebastian Borgeaud, Arthur Mensch, Jordan Hoffmann, Trevor Cai, Eliza Rutherford, Katie Millican, George Bm van den Driessche, Jean-Baptiste Lespiau, Bogdan Damoc, Aidan Clark, et al. Improving language models by retrieving from trillions of tokens. International Conference on Machine Learning , 2022.

- [5] Shouyuan Chen, Sherman Wong, Liangjian Chen, and Yuandong Tian. Extending context window of large language models via positional interpolation. arXiv preprint arXiv:2306.15595 , 2023.
- [6] George Cybenko. Approximation by superpositions of a sigmoidal function. Mathematics of Control, Signals and Systems , 2(4):303-314, 1989.
- [7] Zihang Dai, Zhilin Yang, Yiming Yang, Jaime Carbonell, Quoc V Le, and Ruslan Salakhutdinov. Transformer-XL: Attentive language models beyond a fixed-length context. arXiv preprint arXiv:1901.02860 , 2019.
- [8] Tri Dao, Dan Fu, Stefano Ermon, Atri Rudra, and Christopher Ré. FlashAttention: Fast and memory-efficient exact attention with IO-awareness. Advances in Neural Information Processing Systems , 35, 2022.
- [9] Peter J Denning. The working set model for program behavior. Communications of the ACM , 11(5):323-333, 1968.
- [10] Suyu Ge, Yunan Zhang, Liyuan Liu, Minjia Zhang, Jiawei Han, and Jianfeng Gao. Model tells you what to discard: Adaptive KV cache compression for LLMs. In International Conference on Learning Representations , 2024.
- [11] Alex Graves, Greg Wayne, and Ivo Danihelka. Neural turing machines. arXiv preprint arXiv:1410.5401 , 2014.
- [12] Alex Graves, Greg Wayne, Malcolm Reynolds, Tim Harley, Ivo Danihelka, Agnieszka Grabska-Barwińska, Sergio Gómez Colmenarejo, Edward Grefenstette, Tiago Ramalho, John Agapiou, et al. Hybrid computing using a neural network with dynamic external memory. Nature , 538(7626):471-476, 2016.
- [13] Kurt Hornik. Approximation capabilities of multilayer feedforward networks. Neural Networks , 4(2):251-257, 1991.
- [14] Tim Kraska, Alex Beutel, Ed H Chi, Jeff Dean, and Neoklis Polyzotis. The case for learned index structures. In Proceedings of the ACM SIGMOD International Conference on Management of Data , pages 489-504, 2018.
- [15] Patrick Lewis, Ethan Perez, Aleksandra Piktus, Fabio Petroni, Vladimir Karpukhin, Naman Goyal, Heinrich Küttler, Mike Lewis, Wen-tau Yih, Tim Rocktäschel, Sebastian Riedel, and Douwe Kiela. Retrieval-augmented generation for knowledge-intensive NLP tasks. In Advances in Neural Information Processing Systems , volume 33, 2020.

- [16] Hao Liu, Matei Zaharia, and Pieter Abbeel. Ring attention with blockwise transformers for near-infinite context. arXiv preprint arXiv:2310.01889 , 2023.
- [17] Nelson F Liu, Kevin Lin, John Hewitt, Ashwin Paranjape, Michele Bevilacqua, Fabio Petroni, and Percy Liang. Lost in the middle: How language models use long contexts. Transactions of the Association for Computational Linguistics , 12:157-173, 2024.
- [18] Yuhan Liu, Hanchen Li, Kuntai Du, Jiayi Yao, Yihua Cheng, Yuyang Huang, Shan Lu, Michael Maire, Henry Hoffmann, Ari Holtzman, and Junchen Jiang. CacheGen: Fast context loading for language model applications via KV cache streaming. In ACM SIGCOMM , 2024.
- [19] Nimrod Megiddo and Dharmendra S Modha. ARC: A self-tuning, low overhead replacement cache. In USENIX Conference on File and Storage Technologies (FAST) , pages 115-130, 2003.
- [20] Kai Mei, Zelong Li, Shuyuan Xu, Ruosong Ye, Yingqiang Ge, and Yongfeng Zhang. AIOS: LLM agent operating system. arXiv preprint arXiv:2403.16971 , 2024.
- [21] Tsendsuren Munkhdalai, Manaal Faruqui, and Siddharth Gopal. Leave no context behind: Efficient infinite context transformers with Infini-attention. arXiv preprint arXiv:2404.07143 , 2024.
- [22] Charles Packer, Sarah Wooders, Kevin Lin, Vivian Fang, Shishir G Patil, Ion Stoica, and Joseph E Gonzalez. MemGPT: Towards LLMs as operating systems. arXiv preprint arXiv:2310.08560 , 2023.
- [23] Jack W Rae, Anna Potapenko, Siddhant M Jayakumar, Chloe Hillier, and Timothy P Lillicrap. Compressive transformers for long-range sequence modelling. arXiv preprint arXiv:1911.05507 , 2020.
- [24] Timo Schick, Jane Dwivedi-Yu, Roberto Dessì, Roberta Raileanu, Maria Lomeli, Eric Hambro, Luke Zettlemoyer, Nicola Cancedda, and Thomas Scialom. Toolformer: Language models can teach themselves to use tools. Advances in Neural Information Processing Systems , 36, 2023.
- [25] John Schulman, Filip Wolski, Prafulla Dhariwal, Alec Radford, and Oleg Klimov. Proximal policy optimization algorithms. arXiv preprint arXiv:1707.06347 , 2017.
- [26] Dale Schuurmans. Memory augmented large language models are computationally universal. arXiv preprint arXiv:2301.04589 , 2023.

- [27] Daniel D Sleator and Robert E Tarjan. Amortized efficiency of list update and paging rules. Communications of the ACM , 28(2):202-208, 1985.
- [28] Jason Weston, Sumit Chopra, and Antoine Bordes. Memory networks. arXiv preprint arXiv:1410.3916 , 2014.
- [29] Zhiyong Wu, Chengcheng Han, Zichen Ding, Zhenmin Weng, Zhoumianze Liu, Shunyu Yao, Tao Yu, and Lingpeng Kong. OS-Copilot: Towards generalist computer agents with self-improvement. arXiv preprint arXiv:2402.07456 , 2024.
- [30] Shunyu Yao, Jeffrey Zhao, Dian Yu, Nan Du, Izhak Shafran, Karthik Narasimhan, and Yuan Cao. ReAct: Synergizing reasoning and acting in language models. International Conference on Learning Representations , 2023.
- [31] Manzil Zaheer, Guru Guruganesh, Kumar Avinava Dubey, Joshua Ainslie, Chris Alberti, Santiago Ontanon, Philip Pham, Anirudh Ravula, Qifan Wang, Li Yang, and Amr Ahmed. Big Bird: Transformers for longer sequences. Advances in Neural Information Processing Systems , 33, 2020.