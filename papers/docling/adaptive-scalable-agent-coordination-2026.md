## Towards Adaptive, Scalable, and Robust Coordination of LLM Agents: A Dynamic Ad-Hoc Networking Perspective

Rui Li 1 Zeyu Zhang 1 Xiaohe Bo 1 Quanyu Dai 2 Chaozhuo Li 3 Feng Wen 2 Xu Chen 1 *

## Abstract

Multi-agent architectures built on large language models (LLMs) have demonstrated the potential to realize swarm intelligence through well-crafted collaboration. However, the substantial burden of manual orchestration inherently raises an imperative to automate the design of agentic workflows. We frame such an agent coordination challenge as a classic problem in dynamic ad-hoc networking: How to establish adaptive and reliable communication among a scalable number of agentic hosts? In response to this unresolved dilemma, we introduce RAPS , a reputation-aware publish-subscribe paradigm for adaptive , scalable , and robust coordination of LLM agents. RAPS is grounded in the Distributed Publish-Subscribe Protocol , allowing LLM agents to exchange messages based on their declared intents rather than predefined topologies. Beyond this substrate, RAPS further incorporates two coherent overlays: (i) Reactive Subscription , enabling agents to dynamically refine their intents; and (ii) Bayesian Reputation , empowering each agent with a local watchdog to detect and isolate malicious peers. Extensive experiments over five benchmarks showcase that our design effectively reconciles adaptivity, scalability, and robustness in a unified multi-agent coordination framework.

## 1. Introduction

Multi-agent systems (MAS) (Park et al., 2023; Li et al., 2023; Qian et al., 2023; Hong et al., 2024; Tran et al., 2025) powered by Large Language Models (LLMs) (Brown et al., 2020; Chowdhery et al., 2023; Dubey et al., 2024) have emerged as a promising avenue towards swarm intelligence, allowing specialized LLM agents (Xi et al., 2025) to collaboratively tackle complex tasks beyond the reach of isolated models (Zhuge et al., 2024). Notably, the efficacy of these systems critically hinges on their underlying coordination

1 Gaoling School of Artificial Intelligence, Renmin University of China. 2 Huawei Technologies Ltd. 3 Beijing University of Posts and Telecommunications. ∗ Corresponding Author: Xu Chen.

Figure 1. Illustrations of different MAS coordination paradigms.

<!-- image -->

architectures (Qian et al., 2024). Early manual orchestration inherently incurs substantial engineering overhead and struggles to generalize across shifting task distributions, thereby underscoring the imperative for automatic multi-agent coordination (Zhuge et al., 2024; Zhang et al., 2025b;c; Dang et al., 2025), which empowers the systems to autonomously orchestrate agentic workflows without human intervention.

In this paper, we approach the challenge of automatic coordination through the perspective of computer networking. Given their strong capabilities, LLM agents effectively function as network hosts that can perceive, process, and forward information. Drawing this parallel, the coordination of LLM agents then naturally aligns with dynamic ad-hoc networking (Ramanathan &amp; Redi, 2002; Baldoni &amp; Virgillito, 2005): a set of agentic hosts actively communicate on demand without centralized control, while these hosts may join, leave, or misbehave. Such an analogy directly informs our desiderata: an ideal automatic MAS should (i) adapt to evolving message flow during execution, (ii) scale with dynamic membership discovery, and (iii) remain robust under misbehaving participants. These three requirementsadaptivity , scalability , and robustness -form a tightly coupled design space.

However, existing automatic coordination methods explore only parts of this design space, yet neither resolves the triad simultaneously . Prior works shown in Figure 1(a) typically search for a specific agentic workflow (Zhuge et al., 2024; Hu et al., 2025; Zhang et al., 2025c) or train a parameterized coordination network (Zhang et al., 2025b; Shen et al., 2025) based on an offline validation set. This paradigm imposes a

fixed connectivity pattern independent of actual communication content (i.e., communication-agnostic ), rendering the system blind to the evolving message flow during inference. Moreover, it lacks support for dynamic agent membership, forcing re-orchestration from scratch as agents join or leave. While several recent advances (Ke et al., 2025; Dang et al., 2025; Yuan et al., 2025), as depicted in Figure 1(b), leverage a high-level meta-controller (e.g., a powerful LLM) to enable inference-time orchestration, their reliance on centralized control inherently incurs the single-point-of-failure bottleneck: If the high-authority meta-controller is compromised, the whole network may malfunction or even collapse, without effective mechanisms for recovery. Therefore, a critical dilemma arises: How to reconcile adaptivity, scalability, and robustness within a unified coordination framework, without heavy training overhead or fragile central control?

In light of this dilemma, we introduce RAPS , a ReputationAware Publish-Subscribe paradigm to establish the adaptive, scalable, and robust coordination among LLM-based agents, as shown in Figure 1(c). Instead of enforcing a static topology, RAPS grounds its coordination fabric in a Distributed Publish-Subscribe Protocol (Baldoni &amp; Virgillito, 2005; Frey &amp; Roman, 2007; Yoo et al., 2009), which functionally decouples each agent host into three modules: a Subscriber to state the agent's intents (i.e., subscriptions ); a Publisher to execute the agentic function and generate new messages (i.e., publications ); and a Broker to direct information flow by matching publications with content-aligned subscriptions. This intent-based communication protocol serves as an adaptive substrate that liberates the agents from rigid interactions, fostering an open information marketplace where collaboration emerges spontaneously. Building on the basic substrate, our RAPS framework further integrates two overlay mechanisms: (i) Reactive Subscription , empowering the agents to refine their subscriptions for dynamic intent evolution ; and (ii) Bayesian Reputation , equipping each agent with a local watchdog to assess peer reliability via Bayesian estimation for trustworthy decentralized collaboration . By combining the publish-subscribe substrate and the overlay mechanisms, RAPS effectively reconciles the critical demands of adaptivity, scalability, and robustness within a unified framework.

Our contributions are summarized as follows:

- Perspective: Weframe the MAS coordination problem from a new perspective of dynamic ad-hoc networking, illuminating the key challenge of reconciling adaptivity, scalability, and robustness within a unified framework.
- Communication Substrate: Wepropose a Distributed Publish-Subscribe Protocol as the coordination fabric. This protocol functionally decouples LLM agents into intent-declaring Subscribers and message-generating Publishers, liberating the system from rigid topologies to foster spontaneous content-centric collaboration.
- Overlay Mechanisms: We further design two critical mechanisms to augment the communication substrate: Reactive Subscription and Bayesian Reputation , where the former facilitates dynamic intent evolution and the latter enforces trustworthy decentralized collaboration.
- Evaluation: Extensive experiments across five diverse benchmarks demonstrate that our holistic RAPS framework effectively reconciles adaptivity, scalability, and robustness for multi-agent coordination, with consistent performance improvements over existing methods.

## 2. Background

From LLM Agents to MAS. The evolution of LLMs has catalyzed a paradigm shift from passive response generation to autonomous agents capable of perception, reasoning, and action (Yao et al., 2023; Schick et al., 2023; Shinn et al., 2023). While single-agent models excel at isolated tasks, they typically struggle with complex problems that require diverse expertise or intricate planning (Zhuge et al., 2024). Inspired by the 'Society of Mind' theory (Minsky, 1986), early frameworks such as CAMEL (Li et al., 2023), MetaGPT (Hong et al., 2024), and ChatDev (Qian et al., 2023) employ role-playing mechanisms or standard operating procedures to facilitate collaboration. However, these systems primarily rely on hard-coded rules and linear chains, lacking the flexibility to handle dynamic task requirements.

Communication-Agnostic Automatic Coordination. To mitigate the burden of manual engineering, a growing body of research focuses on automating the coordination of LLM agents. Pioneering works such as GPTSwarm (Zhuge et al., 2024) model agent interactions as computation graphs, employing evolutionary algorithms to search for optimal connectivity patterns on a validation set. Similarly, some frameworks like ADAS (Hu et al., 2025) and AFlow (Zhang et al., 2025c) utilize search-based optimization to discover effective workflows, while G-Designer (Zhang et al., 2025b) and EIB-Learner (Shen et al., 2025) train a graph neural network to model interaction patterns. These methods decouple the topology orchestration from inference-time communication, inherently inducing rigid agentic workflows regardless of the specific inference context. Such a communication-agnostic strategy not only hinders the adaptivity to evolving message flow, but also impedes the scalability of candidate agents, as agent membership changes would render the pre-wired topologies obsolete, incurring substantial re-optimization costs.

Meta-Controlled Automatic Coordination. To address the rigidity of communication-agnostic coordination, a parallel stream of research adopts a centralized control paradigm to govern the agent communication. Recent advances such as AutoAgents (Chen et al., 2023), MAS-Zero (Ke et al., 2025), and Puppeteer (Dang et al., 2025) leverage a high-authority meta-planner to actively oversee the entire agent interactions

Figure 2. The overall framework of RAPS. The architecture consists of three layers: (a) A distributed publish-subscribe substrate that decouples agents into publishers and subscribers for MAS coordination; (b) Reactive subscription that allows agents to adaptively refine their intents based on the message flow; and (3) Bayesian reputation that employs a local watchdog to assess peer reliability for robustness.

<!-- image -->

and direct the message flow during inference. While such an inference-time orchestration strategy enables message-level adaptivity to some extent, its efficacy is tightly bound to the meta-controller's capabilities (Dang et al., 2025), incurring a robustness problem of single-point-of-failure. Furthermore, this centralized dependency also imposes a severe bottleneck on scalability , as the decision space for the meta-controller expands combinatorially as the agent population increases.

In contrast to these methods, RAPS shifts the coordination paradigm from offline topology search or fragile centralized control to a distributed intent-centric communication fabric . By grounding LLM agent interactions in a publish-subscribe substrate enriched with two overlay mechanisms, our RAPS fosters spontaneous collaboration based on dynamic intents and peer trust, effectively reconciling adaptivity, scalability, and robustness within a unified framework. Extended discussions on broader related works are provided in Appendix B.

## 3. LLM Agent Coordination as Dynamic Ad-Hoc Networking

In this section, we formalize the multi-agent coordination problem and establish its correspondence with Dynamic AdHoc Networking . This perspective illuminates three critical desiderata (i.e., Adaptivity , Scalability , and Robustness ) that current coordination methods fail to satisfy simultaneously.

## 3.1. Problem Formulation

We formulate the multi-agent system as a dynamic population of agentic hosts A = { a 1 , . . . , a N } . Each host a i ∈ A operates under an LLM-driven policy π θ i . The operational context of π θ i is encapsulated by a configuration tuple C i = ⟨ S i , H i , T i ⟩ , where S i denotes the system prompt specifying the role or function of a i , H i represents the memory buffer storing the accumulated knowledge and interaction history of a i , and T i is a collection of external tools available to a i . Given a complex task query q , the objective of the system is to synthesize a solution through a collaborative process.

At any discrete time step t , the agent a i observes the current state of its local context H ( t ) i and then generates a message m ( t ) i ∼ π θ i ( ·| S i , H ( t ) i , T i ) . The core challenge of MAS design centers on the coordination function Φ , which governs the dissemination scope of this new message:

<!-- formula-not-decoded -->

When the new message arrives, each recipient a j ∈ R ( t +1) i updates its local context H ( t +1) j ← H ( t ) j ∪{ m ( t ) i } and may subsequently trigger new actions. In contrast to recent works that typically model Φ as either a static topological mapping (i.e., routing messages on communication-agnostic graphs ) or a global orchestration policy (i.e., leveraging a centralized meta-controller to dispatch messages), we rethink the automatic coordination problem from the perspective of dynamic ad-hoc networking , where communication patterns emerge spontaneously based on the semantic intents of each agent.

## 3.2. Networking Perspective

We posit that the coordination of LLM agents is structurally isomorphic to the operation of dynamic ad-hoc networks (Ramanathan &amp; Redi, 2002). Specifically, we map the basic components of LLM-MAS to classic networking primitives:

- Hosts ( a i ): Each LLM agent functions as an autonomous network host. Under this analogy, the system prompt S i of a i defines the host's service profile; the memory state H ( t ) i acts as the local cache for storing interaction history; and the toolset T i represents the executable plugins for service.
- Packets ( m ( t ) i ): The natural language messages generated by agents serve as information packets carrying semantic payloads. Unlike traditional TCP/IP networks that route packets based on explicit destination addresses (Kessler, 2004), the dissemination of m ( t ) i in dynamic ad-hoc networks is governed by the semantic alignment between its payload and the receivers' intents (Ahlgren et al., 2012).
- Topology ( G ( t ) ): The interaction graph G ( t ) induced by Φ naturally reveals a transient communication pattern. In the ad-hoc settings, this topology is decoupled from physical infrastructure, operating instead as a logical overlay that evolves on-the-fly. Links between hosts emerge spontaneously, driven by the shifts in host intents (Yoo et al., 2009) and peer trustworthiness (Buchegger &amp; Le Boudec, 2004).

This mapping also reveals that prevailing MAS orchestration works such as AFlow (Zhang et al., 2025c) and G-Designer (Zhang et al., 2025b) are akin to the legacy circuit-switching networks (Tanenbaum, 2003), which are incompatible with the dynamic information flow of swarm intelligence. To deal with this rigidity, the system necessitates a paradigm shift towards content-centric packet-switching (Eugster et al., 2003), where the routing decisions are decentralized and driven by semantic content rather than pre-established paths.

Design Desiderata. Drawing on the isomorphism between MASand dynamic ad-hoc networking, we posit an effective coordination design to be one that reconciles three critical properties: (i) Adaptivity requires intent-based communication, where message dissemination is governed by semantic alignment rather than fixed topologies that are agnostic to the evolving message flows (Zhuge et al., 2024; Zhang et al., 2025a;b;c); (ii) Scalability demands the support for dynamic membership (Eugster et al., 2003) to accommodate variable agent populations, avoiding the prohibitive re-optimization overhead of pre-wiring models (Zhuge et al., 2024; Zhang et al., 2025a;b;c) and the combinatorial bottlenecks of centralized meta-controllers (Ke et al., 2025; Dang et al., 2025); (iii) Robustness necessitates a decentralized trust mechanism to insulate the system from adversarial behaviors (Shayegani et al., 2023) and alleviate the single-point-of-failure risks (Ke et al., 2025). Refer to Appendix B for more discussions.

## 4. RAPS: Reputation-Aware Publish-Subscribe Communication

In this paper, we introduce RAPS , a new MAS coordination framework that simultaneously reconciles the desiderata of adaptivity, scalability, and robustness outlined in Section 3.

We first provide an overview of our RAPS framework (§4.1). Subsequently, we elaborate on its communication substrate (§4.2) and two critical overlay mechanisms (§4.3 and §4.4).

## 4.1. Overview

As illustrated in Figure 2, RAPS fundamentally shifts the coordination paradigm from rigid topology orchestration or fragile centralized control to intent-centric communication.

At its core, RAPS employs a distributed publish-subscribe protocol as the communication substrate, which functionally decouples the agentic hosts into packet producers and consumers, directing the information flow based on the semantic alignment between declared intents (i.e., subscriptions ) and produced contents (i.e., publications ). By eschewing the prewired interaction graphs and the vulnerable central planners, this substrate fosters an open information marketplace for adaptive MAS coordination, while also allowing the agents to seamlessly join or leave without topology reconfiguration.

Building on such a communication substrate, RAPS further incorporates two overlay mechanisms to facilitate dynamic intent evolution and trustworthy decentralized collaboration. Reactive Subscription empowers the agents to dynamically update their intents based on the evolving message contexts, akin to the online prompt optimization (Li et al., 2025b) that can spontaneously adapt to the unfolding interaction history. Bayesian Reputation equips each agent with a local watchdog that maintains probabilistic beliefs about peer reliability to insulates the system from adversarial behaviors, ensuring robust collaboration without recourse to a central authority. By integrating these overlays atop the distributed substrate, RAPS coherently fulfills the desiderata of adaptivity, scalability, and robustness in a unified coordination framework.

## 4.2. SUBSTRATE: Publish-Subscribe Protocol

To dismantle the rigidity of pre-wired interaction topologies, RAPS moves beyond current MAS designs by establishing a distributed publish-subscribe protocol as its communication substrate. This protocol functionally decouples the agentic hosts into message producers ( Publishers ) and consumers ( Subscribers ), mediated by a content-aware Broker module.

Three Functional Roles. Formally, we define the operational tuple for each agent a i ∈ A as ⟨ f S i , f P i , f B i ⟩ , corresponding to three distinct logical roles :

- Subscriber ( f S i ): This function allows the network host to explicitly declare its packet intents or service capability. In the agentic context, such a subscription naturally aligns with the system prompt S i configured when each agent a i joins the network. For instance, a host initialized with the role ' Python Expert ' directly broadcasts a standing subscription to the network, signaling its ability to process the publications about code generation or debugging tasks.

- Publisher ( f P i ): The publisher acts as the functional core of agent a i . At step t , upon receiving a packet m ( t -1) that aligns with its subscription S i , the publisher directly runs the base policy π θ i conditioned on its local context buffer ⟨ S i , H ( t -1) i ∪ m ( t -1) , T i ⟩ to produce a new message m ( t ) . Note that the publisher does not specify recipients of m ( t ) , but rather broadcasts it as a semantic signal for alignment.

̸

- Broker ( f B i ): This module operates as the instantiation of the coordination function Φ in Equation (1). Unlike prior methods that model Φ as static lookup or central planning, the broker implements Φ through decentralized semantic matching : it derives the recipient set R ( t +1) i by assessing the compatibility between the published message m t and the global subscription pool S = { S j } j = i . Such an intentcontent bridging process fosters the interaction graph G ( t ) to emerge spontaneously in response to the task progress.

Coordination Dynamics. With these three functional roles, the basic coordination protocol of RAPS alternates between Publication and Brokerage to induce the interaction patterns.

Publication Phase. At any time step t , RAPS operates on a set of active agents R ( t ) ∗ . For the initial step ( t = 0 ), R (0) init is populated with default entry roles (e.g., an Analyst agent) activated directly by the input query q , whereas R ( t ) ∗ for the subsequent steps is inherited from the preceding brokerage outcomes. Every active agent a i ∈ R ( t ) ∗ first aggregates the received message packets M ( t -1) i = { m ( t -1) ∗ | a i ∈ R ( t ) ∗ } to update its interaction context. Then, a i runs the publisher function f P i to produce the new publication:

<!-- formula-not-decoded -->

In practice, f P i is typically instantiated as the LLM backbone π θ i of a i . The publication m ( t ) i serves as a flexible semantic container modulated by the output instructions of policy π θ i , capable of explicitly encapsulating the chain-of-thought reasoning (Wei et al., 2022), intermediate execution results, and strategic plans intended for subsequent interactions.

Brokerage Phase. Upon the publication of m ( t ) i , the control flow shifts to the broker f B i to determine dissemination path for this message. The broker treats m ( t ) i as a query to search for compatible consumers within the standing subscriptions S of other hosts. Formally, for each producer a i , its broker identifies a subset of recipients R ( t +1) i :

<!-- formula-not-decoded -->

In practice, the instantiation of f B i spans a spectrum of complexity, ranging from lightweight embedding-based scoring to expressive LLM-driven selection. Such brokers can only determine one recipient each time to produce dynamic chain topology (Dang et al., 2025) for simple scenarios, while also retaining the flexibility to activate multiple subscribers simultaneously for complex problem solving (Li et al., 2025a).

Termination. This publication-brokerage process terminates when the active agents emit a consensus finish signal or the maximum time step limit is reached. Finally, the system aggregates all publications and delegating one terminal agent to provide the final answer (Jiang et al., 2023; Wu et al., 2024; Liu et al., 2024; Zhang et al., 2024; 2025b).

This intent-driven substrate naturally establishes an adaptive and scalable communication fabric by liberating message flow from static topology. Notably, the raw protocol neither accounts for the evolving granularity of agent subscriptions nor guards against potential misbehavior in an open system. To bridge these gaps, we further augment this substrate with two coherent overlay mechanisms: reactive subscription for dynamic intent evolution (§4.3) and Bayesian reputation for trustworthy decentralized collaboration (§4.4).

## 4.3. OVERLAY I: Reactive Subscription

Under the substrate protocol, LLM agents act as static hosts with fixed subscriptions (i.e., system prompts) initialized at their joining time. However, such seed subscriptions may be too abstract or even absent for the emerging message types.

To improve the intent-content alignment without introducing centralized control, RAPS augments the basic substrate with a lightweight mechanism of Reactive Subscription , allowing each active agent to refine its standing subscription based on the newly received packets before the publication phase.

Reactive Subscription. At time step t , each active host a i ∈ R ( t ) ∗ first updates its system profile S ( t -1) i according to the latest messages M ( t -1) i and optionally its context H ( t -1) i :

<!-- formula-not-decoded -->

where f S i is instantiated as an LLM-driven prompt rewriter that specializes the seed subscription by identifying salient intents, constraints, or tool cues from the received messages. Such an updated S ( t ) i then directly conditions the subsequent publication in Equation (2), replacing the static prompt with the reactive one, so that the next message is generated under a more context-aware role to improve the functional quality. Moreover, RAPS can optionally promote the transient local profile S ( t ) i into the global subscription pool S under certain confirmation signals (e.g., peer acknowledgments in the next subsection), thus coherently realizing dynamic membership discovery without any centralized topology reconfiguration.

## 4.4. OVERLAY II: Bayesian Reputation

While the reactive subscription mechanism improves intentcontent alignment, the open-ended communication substrate

of RAPS remains vulnerable to unreliable behaviors such as hallucinations (Huang et al., 2025), tool misuse (Fu et al., 2024), or adversarial manipulation (Shayegani et al., 2023). To secure the coordination without reverting to a centralized global judge, RAPS presents a fully decentralized Bayesian Reputation Overlay inspired by classic robustness designs (Buchegger &amp; Le Boudec, 2002; 2004) in ad-hoc networks.

Distributed Watchdog. RAPS equips each agent a i with a local watchdog that maintains pairwise probabilistic beliefs about every peer a j it interacts with. Concretely, the watchdog of a i holds three probabilistic ratings for a j , including First-Hand Rating F ij that indicates a i 's direct evaluation of a j 's behavioral quality; Trust Rating T ij that quantifies how much a i trusts a j as a reporter of reputation evidences; and the overall reputation P ij that aggregates direct evaluation and indirect reports into the final belief about a j 's reliability.

Bayesian Modeling. Since LLM agents operate with sparse, noisy, and non-stationary context, the point estimates of peer reliability can be brittle. To draw uncertainty-aware beliefs, we cast each rating as a Bayesian posterior over an underlying Bernoulli parameter associated with the evidence stream. Following the classic settings (Davison, 2003; Berger, 2013), we then use the conjugate Beta-Bernoulli model to represent the uncertainty of each rating with two pseudo-counts:

<!-- formula-not-decoded -->

Here F ij and P ij summarize a j 's behavioral reliability as a publication source, while T ij represents the credibility of a j , each driven by its corresponding Bernoulli evidence stream.

All ratings start from a non-informative prior β (1 , 1) , and are updated incrementally as the new observation s ∗ ij arrives. Since agent behaviors and task regimes may drift over time, we adopt a discounted update that emphasizes the recent s ∗ ij :

<!-- formula-not-decoded -->

where s ∗ ij denotes the corresponding binary evidence for the specific rating to be updated (i.e., ∗ ∈ { F, T, R } ); λ ∈ (0 , 1] represents a decay factor applied to the past pseudo-counts. The posterior mean E [ β ( x ∗ ij , y ∗ ij )] = x ∗ ij / ( x ∗ ij + y ∗ ij ) offers a calibrated point estimate, while the concentration ( x ∗ ij + y ∗ ij ) also reflects epistemic uncertainty for downstream decisions.

Reputation Calculation. Based on the Bayesian modeling, RAPS calculates each rating by instantiating the Bernoulli evidence stream during coordination. When a i routes a message to a downstream agent a j , the local watchdog of a i (i) performs a fi rst-hand evaluation on the publication of a j , (ii) selectively collects second-hand testimonials from other witnesses and assesses their credibility to update trust, and (iii) merges admissible direct/indirect evidence into the final reputation posterior that fuels the reputation-aware brokers.

First-Hand Evaluation. Whenever a i receives a publication from a j , the local watchdog of a i (e.g., an LLM auditor)

performs an on-the-fly verification to produce a first-hand binary evaluation score s F ij ∈ { 0 , 1 } . Concretely, s F ij = 0 if the publication of a j is judged useful and consistent with its current context; otherwise s F ij = 1 if the watchdog detects qualified misbehaviors such as factual error and tool misuse:

<!-- formula-not-decoded -->

where f D i denotes the watchdog function of a i . The evidence s F ij is then used to update the first-hand posterior F ij = β ( x F ij , y F ij ) via the discounted rule in Equation (6), yielding a direct rationality summary of a j from the perspective of a i .

Second-Hand Witness. While first-hand evaluation anchors reputation to verifiable local evidence, relying solely on s F ij can be sample-inefficient in open coordination (Michiardi &amp;Molva, 2002). To accelerate the discovery of unreliable peers, RAPS allows a i to optionally solicit second-hand testimonies from other witnesses that have recently interacted with a j . Concretely, upon receiving m ( t ) j , a i queries a small set of witnesses W ( t ) ij ⊆ A \ { a i , a j } and obtains their firsthand posteriors about a j , i.e., { F kj = β ( x F kj , y F kj ) } a k ∈W ( t ) ij . Since second-hand reports may be noisy or malicious, a i evaluates each witness a k by performing a deviation test (Buchegger &amp; Le Boudec, 2004) on its current belief P ij :

<!-- formula-not-decoded -->

where δ is a predefined tolerance threshold. If the deviation exceeds δ , the testimony is deemed incompatible (yielding evidence s T ik = 1 ); otherwise, it is accepted ( s T ik = 0 ). This binary evidence is then used to update the trust posterior T ik via Eq. (6), progressively isolating dishonest witnesses.

Reputation Merging. To synthesize a holistic view of peer reliability, a i aggregates its direct evidence s F ij with indirect testimonies into the cumulative reputation posterior P ij via linear pool model merging (Berger, 2013). First, P ij is anchored to the first-hand evidence s F ij , directly updating the parameters ( x P ij , y P ij ) with Eq. (6). Then, a i incorporates second-hand reports from a subset of admissible witnesses W ∗ ij ⊆ W ( t ) ij . Following the classic protocol (Buchegger &amp; Le Boudec, 2004), a witness a k is deemed admissible if it is either locally trusted (i.e., E [ T ik ] ≥ τ ) or its testimony successfully passes the deviation test (i.e., s T ik = 0 ). For every a k ∈ W ∗ ij , we assimilate its reported posterior F kj = β ( x F kj , y F kj ) as weak pseudo-counts to refine the local belief:

<!-- formula-not-decoded -->

where ω ∈ (0 , 1) is a discounting factor. Such a merging mechanism ensures that indirect evidence can modulate but does not overwhelm first-hand observations, resulting in a trust-gated aggregation that is robust to reputation poisoning.

Table 1. Performance comparisons with four types of baselines, including single-agent models, static multi-agent models, communicationagnostic models, and meta-controlled models. The best results are in bold , and the second best results are underlined. All methods, except for the single-agent models, employ fi ve identically configured agents for fair comparisons.

| Method                                | MMLU                                  | GSM8K        | SVAMP        | AQuA          | HumanEval     | Average      |
|---------------------------------------|---------------------------------------|--------------|--------------|---------------|---------------|--------------|
| Single-Agent Models                   |                                       |              |              |               |               |              |
| Vanilla IO (GPT-4o-mini)              | 81.7                                  | 91.6         | 87.5         | 71.3          | 72.8          | 81.0         |
| CoT (Wei et al., 2022)                | 83.0 ↑ 1 . 3                          | 92.1 ↑ 0 . 5 | 88.4 ↑ 0 . 9 | 74.7 ↑ 3 . 4  | 75.7 ↑ 2 . 9  | 82.8 ↑ 1 . 8 |
| ComplexCoT (Fu et al., 2022)          | 83.7 ↑ 2 . 0                          | 92.5 ↑ 0 . 9 | 89.2 ↑ 1 . 7 | 76.1 ↑ 4 . 8  | 75.2 ↑ 2 . 4  | 83.3 ↑ 2 . 3 |
| SC (Wang et al., 2022)                | 82.4 ↑ 0 . 7                          | 92.4 ↑ 0 . 8 | 88.9 ↑ 1 . 4 | 76.8 ↑ 5 . 5  | 77.5 ↑ 4 . 7  | 83.6 ↑ 2 . 6 |
| Static Multi-Agent Models             |                                       |              |              |               |               |              |
| Chain (Qian et al., 2024)             | 84.3 ↑ 2 . 6                          | 91.7 ↑ 0 . 1 | 82.6 ↓ 4 . 9 | 70.4 ↓ 0 . 9  | 81.3 ↑ 8 . 5  | 82.1 ↑ 1 . 1 |
| Star (Qian et al., 2024)              | 80.4 ↓ 1 . 3                          | 91.9 ↑ 0 . 3 | 88.2 ↑ 0 . 7 | 69.6 ↓ 1 . 7  | 74.5 ↑ 1 . 7  | 80.9 ↓ 0 . 1 |
| Tree (Qian et al., 2024)              | 82.4 ↑ 0 . 7                          | 90.7 ↓ 0 . 9 | 88.5 ↑ 1 . 0 | 73.9 ↑ 2 . 6  | 72.4 ↓ 0 . 4  | 81.6 ↑ 0 . 6 |
| Random (Qian et al., 2024)            | 85.6 ↑ 3 . 9                          | 92.0 ↑ 0 . 4 | 87.0 ↓ 0 . 5 | 75.1 ↑ 3 . 8  | 78.2 ↑ 5 . 4  | 83.6 ↑ 2 . 6 |
| LLM-Debate (Du et al., 2023)          | 85.0 ↑ 3 . 3                          | 92.4 ↑ 0 . 8 | 89.8 ↑ 2 . 3 | 77.3 ↑ 6 . 0  | 82.6 ↑ 9 . 8  | 85.4 ↑ 4 . 4 |
| LLM-Blender (Jiang et al., 2023)      | 81.0 ↓ 0 . 7                          | 91.3 ↓ 0 . 3 | 88.3 ↑ 0 . 8 | 76.9 ↑ 5 . 6  | -             | 84.4 ↑ 3 . 4 |
| Communication-Agnostic Models         |                                       |              |              |               |               |              |
| GPTSwarm (Zhuge et al., 2024)         | 83.7 ↑ 2 . 0                          | 92.7 ↑ 1 . 1 | 88.5 ↑ 1 . 0 | 78.2 ↑ 6 . 9  | 88.5 ↑ 15 . 7 | 86.3 ↑ 5 . 3 |
| AgentPrune (Zhang et al., 2024)       | 84.3 ↑ 2 . 6                          | 92.3 ↑ 0 . 7 | 89.8 ↑ 2 . 3 | 79.1 ↑ 7 . 8  | 86.8 ↑ 14 . 0 | 86.5 ↑ 5 . 5 |
| AFlow (Zhang et al., 2025c)           | 85.6 ↑ 3 . 9                          | 94.1 ↑ 2 . 5 | 90.0 ↑ 2 . 5 | 78.5 ↑ 7 . 2  | 91.0 ↑ 18 . 2 | 87.8 ↑ 6 . 8 |
| MaAS (Zhang et al., 2025a)            | 85.0 ↑ 3 . 3                          | 91.4 ↓ 0 . 2 | 89.3 ↑ 1 . 8 | 76.2 ↑ 4 . 9  | 87.1 ↑ 14 . 3 | 85.8 ↑ 4 . 8 |
| G-Designer (Zhang et al., 2025b)      | 86.3 ↑ 4 . 6                          | 93.2 ↑ 1 . 6 | 90.7 ↑ 3 . 2 | 79.4 ↑ 8 . 1  | 90.2 ↑ 17 . 4 | 88.0 ↑ 7 . 0 |
| Meta-Controlled Models                |                                       |              |              |               |               |              |
| AutoAgents (Chen et al., 2023)        | 82.4 ↑ 0 . 7                          | 92.5 ↑ 0 . 9 | 86.7 ↓ 0 . 8 | 75.7 ↑ 4 . 4  | 87.6 ↑ 14 . 8 | 85.0 ↑ 4 . 0 |
| Puppeteer (Dang et al., 2025)         | 84.3 ↑ 2 . 6                          | 93.3 ↑ 1 . 7 | 89.5 ↑ 2 . 0 | 77.5 ↑ 6 . 2  | 75.3 ↑ 2 . 5  | 84.0 ↑ 3 . 0 |
| MAS-Zero (Ke et al., 2025)            | 83.0 ↑ 1 . 3                          | 92.6 ↑ 1 . 0 | 87.3 ↓ 0 . 2 | 72.9 ↑ 1 . 6  | 83.9 ↑ 11 . 1 | 83.9 ↑ 2 . 9 |
| Dynamic Ad-Hoc Networking Perspective | Dynamic Ad-Hoc Networking Perspective |              |              |               |               |              |
| RAPS (Ours)                           | 88.2 ↑ 6 . 5                          | 95.4 ↑ 3 . 8 | 92.2 ↑ 4 . 7 | 82.6 ↑ 11 . 3 | 91.5 ↑ 18 . 7 | 90.0 ↑ 9 . 0 |

Figure 3. Scalability and Efficiency analysis with varying #agents.

<!-- image -->

Reputation-Aware Brokerage. With the reputation overlay, the broker of a i evolves to be reliability-aware, thus enabling us to rewrite the brokerage function in Equation (3) as:

<!-- formula-not-decoded -->

where P i denotes a i 's local reputation table. As a result, the information flow is steered away from untrusted hosts, preventing local failures from cascading into global corruption.

## 5. Experiments

To comprehensively validate the effectiveness of our RAPS framework, we conduct extensive experiments on a range of representative agent tasks spanning general reasoning, mathematical problem solving, and code generation. In the main

Figure 4. Cost-Performance analysis of different MAS coordination methods. k represents the communication rounds for RAPS.

<!-- image -->

experiment (§5.2), we compare RAPS with strong baselines over five heterogeneous benchmarks to assess its adaptivity. Then, we vary the number of agents to study scalability and efficiency under open-ended memberships (§5.3). Next, we stress-test robustness (§5.4) by injecting adversarial agents. Finally, the detailed mechanism analysis is provided in §5.5.

## 5.1. Experimental Setup

Benchmarks. Weevaluate RAPS over five standard datasets from three categories: MMLU (Hendrycks et al., 2020) for general reasoning; GSM8K (Cobbe et al., 2021), SVAMP (Patel et al., 2021), and AQuA (Ling et al., 2017) for mathematical reasoning; HumanEval (Chen et al., 2021) for code generation. We report Pass@1 for HumanEval and accuracy (Acc.) for others. See Appendix D for the detailed statistics.

Table 2. Robustness to After-Training prompt attacks on MMLU. 'T' and 'A' represent truthful and adversarial agents, respectively.

| Method      |   5T0A |   4T1A | 3T2A   | 2T3A   | 5T5A   |
|-------------|--------|--------|--------|--------|--------|
| Chain       |   84.3 |   72.5 | 50.3   | 22.2   | 16.3   |
| Random      |   85.6 |   81.7 | 35.3   | 18.3   | 45.1   |
| LLM-Debate  |   85   |   78.4 | 62.1   | 30.7   | 47.7   |
| GPTSwarm    |   83.7 |   75.2 | 55.6   | 23.5   | 52.9   |
| AFlow       |   85.6 |   79.7 | 52.3   | 19.6   | 28.8   |
| G-Designer  |   86.3 |   80.4 | 37.9   | 15.0   | 49.7   |
| Puppeteer-P |   84.3 |   77.8 | 65.4   | 32.0   | 51.6   |
| Puppeteer-C |   84.3 |   13.7 | -      | -      | -      |
| RAPS w/o BR |   86.9 |   83.7 | 69.3   | 33.3   | 53.6   |
| RAPS        |   88.2 |   87.6 | 84.3   | 83.0   | 86.3   |

Baselines. We compare RAPS with four types of baselines, including (1) Single-Agent Models : CoT (Wei et al., 2022), ComplexCoT (Fu et al., 2022), and Self-Consistency (abbr. SC) (Wang et al., 2022); (2) Static Multi-Agent Models : MacNet (Chain, Star, Tree, Random) (Qian et al., 2024), LLM-Debate (Du et al., 2023), and LLM-Blender (Jiang et al., 2023); (3) Communication-Agnostic Coordination : GPTSwarm (Zhuge et al., 2024), AgentPrune (Zhang et al., 2024), AFlow (Zhang et al., 2025c), MaAS (Zhang et al., 2025a), and G-Designer (Zhang et al., 2025b); (4) MetaControlled Coordination : AutoAgents (Chen et al., 2023), Puppeteer (Dang et al., 2025), MAS-Zero (Ke et al., 2025).

Implementation Details. Unless otherwise specified, we use GPT-4o-mini as the LLM backbone model across all methods to ensure fair comparisons. For the agentic profiles in MAS, we adopt the standard role definitions and system prompt configurations following previous works (Liu et al., 2024; Zhuge et al., 2024). For our RAPS framework, we instantiate the publishers f P i , reactive subscribers f S i , and local watchdogs f D i using the same LLM backbone. The broker f B i is implemented to support both embeddingbased matching with text-embedding-3-small for efficiency and LLM-based reasoning for complex decisions. More implementation details are provided in Appendix E.

## 5.2. Main Results

The performance comparison on five benchmarks is reported in Table 1. RAPS achieves state-of-the-art performance across all tasks, attaining the highest average score of 90.0% . We attribute these advanced gains to the message-level adaptivity of RAPS compared to existing paradigms:

Superiority over Static Structures. The static models (e.g., Chain, Star) often lag behind even single-agent baselines (e.g., Chain is 4.9% lower than CoT on SVAMP), indicating that rigid topologies cannot accommodate the diverse reasoning patterns required by different queries. RAPS overcomes this limitation by allowing the interaction graph to emerge dynamically. The intent-based publish-subscribe substrate ensures that messages are routed to relevant agents, avoiding the structural mismatch inherent in static graphs.

Table 3. Ablation study on different variants of RAPS.

| Variant             | MMLU          | GSM8K         | HumanEval     |
|---------------------|---------------|---------------|---------------|
| RAPS (Full)         | 88.2          | 95.4          | 91.5          |
| w/o RS              | 85.6 ( ↓ 2.6) | 93.7 ( ↓ 1.7) | 89.3 ( ↓ 2.2) |
| w/o BR              | 86.9 ( ↓ 1.3) | 94.5 ( ↓ 0.9) | 90.7 ( ↓ 0.8) |
| w/o Both            | 83.7 ( ↓ 4.5) | 92.8 ( ↓ 2.6) | 88.5 ( ↓ 3.0) |
| w/ LLM Broker       | 89.5          | 95.9          | 93.7          |
| w/ Naive Agent Pool | 85.0          | 93.5          | 90.2          |

Advantages over Offline Optimization. Communicationagnostic methods such as AFlow and G-Designer search for or generate workflows without considering specific communication process. Despite their effectiveness, such methods remain blind to the evolving message flow during inference. Beyond these baselines, RAPS framework achieves superior performance (e.g., +3.2% over G-Designer on AQuA) with its inherent capacity for inference-time coordination. Moreover, the agents in RAPS can further tailor their roles to the message context via reactive subscription , thereby capturing subtle reasoning paths overlooked by the pre-wired models.

Comparison with Centralized Control. Meta-controlled architectures (e.g., AutoAgents, Puppeteer, and MAS-Zero) rely on a central planner, inherently inducing a performance bottleneck due to the complexity of global state supervision. RAPS surpasses these models (e.g., +5.0% over AutoAgents on average) by distributing the routing logic. Departing from a monolithic agent that dictates holistic connectivity, RAPS decentralizes the routing logic by empowering local brokers to adaptively align publications with relevant subscriptions, leading to more nuanced and effective collaboration patterns.

## 5.3. Scalability and Efficiency

Acritical requirement for dynamic ad-hoc networking is the ability to scale effectively as the number of hosts increases. We evaluate the scalability and efficiency of RAPS by varying the agent population size N on the MMLU benchmark.

Population Scalability. Figure 3(a) shows the performance (accuracy) of each method as the agent population increases. The chain model suffers from severe accuracy degradation due to error propagation across increasing interaction hops. Similarly, the Puppeteer model also fails to maintain performance, since its central orchestrator struggles to analyze and coordinate among the growing number of agents. In contrast, RAPS consistently outperforms such approaches, exhibiting steady accuracy gains with the increasing number of agents. This demonstrates the scalability of our distributed publishsubscribe protocol, confirming its ability to handle growing agent populations without compromising performance.

Communication Efficiency. Figure 3(b) further reports the end-to-end runtime as the agent population scales. The two communication-agnostic approaches (i.e., GPTSwarm and G-Designer) inherently incur rapidly growing optimization overhead (over 2 hours for 20 agents) as the agent candidate

Figure 5. Impact of agent pool quality on HumanEval benchmark.

<!-- image -->

pool expands. In comparison, RAPS is training-free and performs coordination entirely at inference time via lightweight publish-subscribe matching. Moreover, the broker -selective dissemination effectively prevents unnecessary interactions, thereby keeping the inference latency stable as N increases. As a result, RAPS achieves favorable runtime scaling while simultaneously improving accuracy, demonstrating an efficient coordination substrate for open-membership systems.

Cost-Performance Trade-off. Furthermore, we analyze the economic viability of our RAPS in Figure 4, which plots the Pareto frontier of accuracy versus token consumption on three representative benchmarks. The green curve presents the performance of RAPS at varying communication rounds k ∈ { 3 , 5 , 7 , 10 } . One can observe that RAPS establishes a superior frontier compared to the state-of-the-art baselines, highlighting its advantages in both accuracy and efficiency.

## 5.4. Robustness Analysis

In open multi-agent systems, agents may exhibit unreliable behaviors due to hallucinations or adversarial attacks. To evaluate robustness, we conduct a 'Byzantine' stress test on the MMLU benchmark by injecting adversarial agents prompted to provide misleading reasoning. We vary the composition of the agent pool from clean (5 Truthful, 0 Adversarial) to highly contaminated (2 Truthful, 3 Adversarial, or a mix of 5T5A). The results are summarized in Table 2.

Fragility of Baselines. Existing models exhibit severe vulnerability to adversarial interference. For instance, AFlow and GPTSwarm experience sharp drops in accuracy when adversarial agents are introduced, with accuracy plummeting to as low as 19.6% under the 2T3A scenario. Moreover, Puppeteer, which relies on a centralized meta-controller, is susceptible to adversarial attacks in two different configurations: one where the adversarial prompt targets the noncentral agents, and another where the adversarial prompt is aimed at the central meta-controller itself. In the latter case, we observe that Puppeteer-C collapses immediately (84.3% → 13.7%) with just one adversarial agent (4T1A), confirming that entrusting a monolithic high-authority controller is inherently unsafe for open coordination.

Efficacy of Bayesian Reputation. In contrast, RAPS consistently outperforms all other methods, maintaining high accuracy even as the number of adversarial agents increases. The performance of RAPS with and without Bayesian Reputation (BR) shows that the inclusion of BR can significantly enhance communication robustness, ensuring stable performance even in the contaminated agent pools. These results demonstrate the effectiveness of BR in maintaining system reliability under adversarial conditions.

## 5.5. Ablation Analysis

To understand the contribution of each component within RAPS, we conduct an ablation study on MMLU, GSM8K, and HumanEval, as presented in Table 3 and Figure 5.

Impact of Overlay Mechanisms. Weevaluate the impact of removing the two designed overlay mechanisms: Reactive Subscription (RS) and Bayesian Reputation (BR). As shown in Table 3, removing RS leads to a notable performance drop, highlighting the importance of dynamic subscriptions in adapting to the task dynamics; similarly, removing BR also incurs a moderate decline, as the reputation-aware brokerage can enhance the communication quality by filtering out lowquality reasoning signals. Therefore, these two mechanisms contribute synergistically to the full performance of RAPS.

Broker Instantiation. We compare the default embeddingbased broker with a more expressive LLM-based broker ( w/ LLM Broker ). While the LLM broker yields marginal performance improvements (e.g., +1.3% on MMLU), it would inherently incur higher communication latency. This justifies our design choice of using the embedding-based broker for the main experiments to balance efficiency and accuracy.

Resilience to Agent Pool Quality. A common limitation of current models is their sensitivity to the quality of initial agent configurations. We investigate this by comparing the model performance on a Crafted Pool (expert roles) versus a Naive Pool (generic roles). As shown in Figure 5, existing models suffer severe degradation when using the naive pool. For instance, G-Designer drops by 9.5% (91.0% → 81.5%). In contrast, RAPS exhibits remarkable resilience, maintaining robust performance with only a negligible 1.3% drop (91.5% → 90.2%). This resilience is directly attributable to the Reactive Subscription mechanism, which allows even naively initialized agents to autonomously refine their intents and specialize their roles based on the message context.

## 6. Conclusion

This paper re-envisions the coordination of LLM agents through the lens of dynamic ad-hoc networking, identifying the critical challenge of simultaneously achieving adaptivity, scalability, and robustness. To address this dilemma, we introduced RAPS , a decentralized framework grounded in a

reputation-aware publish-subscribe protocol. By decoupling coordination from rigid topologies and fragile centralized controllers, RAPS empowers agents to spontaneously align their intents with evolving tasks and autonomously isolate unreliable peers via a distributed trust mechanism. Extensive experiments across five benchmarks demonstrate that RAPS effectively reconciles the coordination triad, yielding state-of-the-art performance while scaling efficiently to larger agent populations and exhibiting superior resilience against adversarial interference. We believe this networkingcentric perspective provides a robust foundation for building open, self-organizing, and trustworthy multi-agent societies.

## Impact Statement

This paper presents work whose goal is to advance the field of Machine Learning. There are many potential societal consequences of our work, none which we feel must be specifically highlighted here.

## References

Ahlgren, B., Dannewitz, C., Imbrenda, C., Kutscher, D., and Ohlman, B. A survey of information-centric networking. IEEE Communications Magazine , 50(7):26-36, 2012.

- Baldoni, R. and Virgillito, A. Distributed event routing in publish/subscribe communication systems: a survey. DIS, Universita di Roma La Sapienza, Tech. Rep , 5, 2005.

Berger, J. O. Statistical decision theory and Bayesian analysis . Springer Science &amp; Business Media, 2013.

Brown, T., Mann, B., Ryder, N., Subbiah, M., Kaplan, J. D., Dhariwal, P., Neelakantan, A., Shyam, P., Sastry, G., Askell, A., Agarwal, S., Herbert-Voss, A., Krueger, G., Henighan, T., Child, R., Ramesh, A., Ziegler, D., Wu, J., Winter, C., Hesse, C., Chen, M., Sigler, E., Litwin, M., Gray, S., Chess, B., Clark, J., Berner, C., McCandlish, S., Radford, A., Sutskever, I., and Amodei, D. Language models are few-shot learners. In Advances in Neural Information Processing Systems , volume 33, pp. 18771901, 2020.

- Buchegger, S. and Le Boudec, J.-Y. Performance analysis of the confidant protocol. In Proceedings of the 3rd ACM international symposium on Mobile ad hoc networking &amp; computing , pp. 226-236, 2002.
- Buchegger, S. and Le Boudec, J.-Y. A robust reputation system for peer-to-peer and mobile ad-hoc networks. P2PEcon 2004 , 2004.
- Chen, G., Dong, S., Shu, Y., Zhang, G., Sesay, J., Karlsson, B. F., Fu, J., and Shi, Y. Autoagents: A framework for automatic agent generation. arXiv preprint arXiv:2309.17288 , 2023.

Chen, M., Tworek, J., Jun, H., Yuan, Q., de Oliveira Pinto, H. P., Kaplan, J., Edwards, H., Burda, Y., Joseph, N., Brockman, G., Ray, A., Puri, R., Krueger, G., Petrov, M., Khlaaf, H., Sastry, G., Mishkin, P., Chan, B., Gray, S., Ryder, N., Pavlov, M., Power, A., Kaiser, L., Bavarian, M., Winter, C., Tillet, P., Such, F. P., Cummings, D., Plappert, M., Chantzis, F., Barnes, E., Herbert-Voss, A., Guss, W. H., Nichol, A., Paino, A., Tezak, N., Tang, J., Babuschkin, I., Balaji, S., Jain, S., Saunders, W., Hesse, C., Carr, A. N., Leike, J., Achiam, J., Misra, V., Morikawa, E., Radford, A., Knight, M., Brundage, M., Murati, M., Mayer, K., Welinder, P., McGrew, B., Amodei, D., McCandlish, S., Sutskever, I., and Zaremba, W. Evaluating large language models trained on code. CoRR , abs/2107.03374, 2021.

- Chowdhery, A., Narang, S., Devlin, J., Bosma, M., Mishra, G., Roberts, A., Barham, P., Chung, H. W., Sutton, C., Gehrmann, S., Schuh, P., Shi, K., Tsvyashchenko, S., Maynez, J., Rao, A., Barnes, P., Tay, Y., Shazeer, N., Prabhakaran, V., Reif, E., Du, N., Hutchinson, B., Pope, R., Bradbury, J., Austin, J., Isard, M., Gur-Ari, G., Yin, P., Duke, T., Levskaya, A., Ghemawat, S., Dev, S., Michalewski, H., Garcia, X., Misra, V., Robinson, K., Fedus, L., Zhou, D., Ippolito, D., Luan, D., Lim, H., Zoph, B., Spiridonov, A., Sepassi, R., Dohan, D., Agrawal, S., Omernick, M., Dai, A. M., Pillai, T. S., Pellat, M., Lewkowycz, A., Moreira, E., Child, R., Polozov, O., Lee, K., Zhou, Z., Wang, X., Saeta, B., Diaz, M., Firat, O., Catasta, M., Wei, J., Meier-Hellstern, K., Eck, D., Dean, J., Petrov, S., and Fiedel, N. Palm: Scaling language modeling with pathways. Journal of Machine Learning Research , 24:240:1-240:113, 2023.

Cobbe, K., Kosaraju, V., Bavarian, M., Chen, M., Jun, H., Kaiser, L., Plappert, M., Tworek, J., Hilton, J., Nakano, R., et al. Training verifiers to solve math word problems. arXiv preprint arXiv:2110.14168 , 2021.

Dang, Y., Qian, C., Luo, X., Fan, J., Xie, Z., Shi, R., Chen, W., Yang, C., Che, X., Tian, Y., Xiong, X., Han, L., Liu, Z., and Sun, M. Multi-agent collaboration via evolving orchestration. In The Thirty-ninth Annual Conference on Neural Information Processing Systems , 2025.

Davison, A. C. Statistical models , volume 11. Cambridge university press, 2003.

Du, Y., Li, S., Torralba, A., Tenenbaum, J. B., and Mordatch, I. Improving factuality and reasoning in language models through multiagent debate. In Forty-first International Conference on Machine Learning , 2023.

Dubey, A., Jauhri, A., Pandey, A., Kadian, A., Al-Dahle, A., Letman, A., Mathur, A., Schelten, A., Yang, A., Fan, A., et al. The llama 3 herd of models. arXiv preprint arXiv:2407.21783 , 2024.

- Estornell, A. and Liu, Y. Multi-llm debate: Framework, principals, and interventions. Advances in Neural Information Processing Systems , 37:28938-28964, 2024.
- Eugster, P. T., Felber, P. A., Guerraoui, R., and Kermarrec, A.-M. The many faces of publish/subscribe. ACM computing surveys (CSUR) , 35(2):114-131, 2003.
- Frey, D. and Roman, G.-C. Context-aware publish subscribe in mobile ad hoc networks. In International Conference on Coordination Languages and Models , pp. 37-55. Springer, 2007.
- Fu, X., Li, S., Wang, Z., Liu, Y., Gupta, R. K., BergKirkpatrick, T., and Fernandes, E. Imprompter: Tricking llm agents into improper tool use. arXiv preprint arXiv:2410.14923 , 2024.
- Fu, Y., Peng, H., Sabharwal, A., Clark, P., and Khot, T. Complexity-based prompting for multi-step reasoning. arXiv preprint arXiv:2210.00720 , 2022.
- Hendrycks, D., Burns, C., Basart, S., Zou, A., Mazeika, M., Song, D., and Steinhardt, J. Measuring massive multitask language understanding. arXiv preprint arXiv:2009.03300 , 2020.
- Hong, S., Zhuge, M., Chen, J., Zheng, X., Cheng, Y., Wang, J., Zhang, C., Wang, Z., Yau, S. K. S., Lin, Z., Zhou, L., Ran, C., Xiao, L., Wu, C., and Schmidhuber, J. MetaGPT: Meta programming for a multi-agent collaborative framework. In The Twelfth International Conference on Learning Representations , 2024.
- Hu, S., Lu, C., and Clune, J. Automated design of agentic systems. In The Thirteenth International Conference on Learning Representations , 2025.
- Huang, L., Yu, W., Ma, W., Zhong, W., Feng, Z., Wang, H., Chen, Q., Peng, W., Feng, X., Qin, B., et al. A survey on hallucination in large language models: Principles, taxonomy, challenges, and open questions. ACM Transactions on Information Systems , 43(2):1-55, 2025.
- Jiang, D., Ren, X., and Lin, B. Y . Llm-blender: Ensembling large language models with pairwise ranking and generative fusion. arXiv preprint arXiv:2306.02561 , 2023.
- Ke, Z., Xu, A., Ming, Y., Nguyen, X.-P., Xiong, C., and Joty, S. Mas-zero: Designing multi-agent systems with zero supervision. arXiv preprint arXiv:2505.14996 , 2025.
- Kessler, G. C. An overview of tcp/ip protocols and the internet. InterNIC Document, Dec , 29:42, 2004.
- Kleinrock, L. and Kamoun, F. Hierarchical routing for large networks performance evaluation and optimization. Computer Networks (1976) , 1(3):155-174, 1977.
- Kreutz, D., Ramos, F. M., Verissimo, P. E., Rothenberg, C. E., Azodolmolky, S., and Uhlig, S. Software-defined networking: A comprehensive survey. Proceedings of the IEEE , 103(1):14-76, 2014.
- Langley, P. Crafting papers on machine learning. In Langley, P. (ed.), Proceedings of the 17th International Conference on Machine Learning (ICML 2000) , pp. 1207-1216, Stanford, CA, 2000. Morgan Kaufmann.
- Li, G., Hammoud, H., Itani, H., Khizbullin, D., and Ghanem, B. Camel: Communicative agents for' mind' exploration of large language model society. In Advances in Neural Information Processing Systems , volume 36, pp. 5199152008, 2023.
- Li, R., Dai, Q., Zhang, Z., Chen, X., Dong, Z., and Wen, J.-R. Knowtrace: Bootstrapping iterative retrievalaugmented generation with structured knowledge tracing. In Proceedings of the 31st ACM SIGKDD Conference on Knowledge Discovery and Data Mining V. 2 , pp. 14701480, 2025a.
- Li, W., Wang, X., Li, W., and Jin, B. A survey of automatic prompt engineering: An optimization perspective. arXiv preprint arXiv:2502.11560 , 2025b.
- Ling, W., Yogatama, D., Dyer, C., and Blunsom, P. Program induction by rationale generation: Learning to solve and explain algebraic word problems. arXiv preprint arXiv:1705.04146 , 2017.
- Liu, Z., Zhang, Y., Li, P., Liu, Y ., and Yang, D. A dynamic LLM-powered agent network for task-oriented agent collaboration. In First Conference on Language Modeling , 2024.
- Low, S. H., Paganini, F., and Doyle, J. C. Internet congestion control. IEEE control systems magazine , 22(1):28-43, 2002.
- Michiardi, P. and Molva, R. Core: a collaborative reputation mechanism to enforce node cooperation in mobile ad hoc networks. In Advanced Communications and Multimedia Security: IFIP TC6/TC11 Sixth Joint Working Conference on Communications and Multimedia Security September 26-27, 2002, Portoroˇ z, Slovenia , pp. 107-121. Springer, 2002.
- Minsky, M. Society of mind . Simon and Schuster, 1986.
- Park, J. S., O'Brien, J., Cai, C. J., Morris, M. R., Liang, P., and Bernstein, M. S. Generative agents: Interactive simulacra of human behavior. In Proceedings of the 36th annual acm symposium on user interface software and technology , pp. 1-22, 2023.

- Patel, A., Bhattamishra, S., and Goyal, N. Are nlp models really able to solve simple math word problems? arXiv preprint arXiv:2103.07191 , 2021.
- Qian, C., Cong, X., Yang, C., Chen, W., Su, Y., Xu, J., Liu, Z., and Sun, M. Communicative agents for software development. arXiv preprint arXiv:2307.07924 , 6(3):1, 2023.
- Qian, C., Xie, Z., Wang, Y., Liu, W., Zhu, K., Xia, H., Dang, Y., Du, Z., Chen, W., Yang, C., et al. Scaling large language model-based multi-agent collaboration. arXiv preprint arXiv:2406.07155 , 2024.
- Ramanathan, R. and Redi, J. A brief overview of ad hoc networks: challenges and directions. IEEE communications Magazine , 40(5):20-22, 2002.
- Schick, T., Dwivedi-Yu, J., Dess` ı, R., Raileanu, R., Lomeli, M., Hambro, E., Zettlemoyer, L., Cancedda, N., and Scialom, T. Toolformer: Language models can teach themselves to use tools. In Advances in Neural Information Processing Systems , volume 36, pp. 68539-68551, 2023.
- Shayegani, E., Mamun, M. A. A., Fu, Y., Zaree, P., Dong, Y., and Abu-Ghazaleh, N. Survey of vulnerabilities in large language models revealed by adversarial attacks. arXiv preprint arXiv:2310.10844 , 2023.
- Shen, X., Liu, Y., Dai, Y., Wang, Y., Miao, R., Tan, Y., Pan, S., and Wang, X. Understanding the information propagation effects of communication topologies in llm-based multi-agent systems. arXiv preprint arXiv:2505.23352 , 2025.
- Shinn, N., Cassano, F., Gopinath, A., Narasimhan, K., and Yao, S. Reflexion: Language agents with verbal reinforcement learning. In Advances in Neural Information Processing Systems , pp. 8634-8652, 2023.
- Tanenbaum, A. S. Computer networks . Pearson Education India, 2003.
- Tran, K.-T., Dao, D., Nguyen, M.-D., Pham, Q.-V., O'Sullivan, B., and Nguyen, H. D. Multi-agent collaboration mechanisms: A survey of llms. arXiv preprint arXiv:2501.06322 , 2025.
- Wang, X., Wei, J., Schuurmans, D., Le, Q., Chi, E., Narang, S., Chowdhery, A., and Zhou, D. Self-consistency improves chain of thought reasoning in language models. arXiv preprint arXiv:2203.11171 , 2022.
- Wei, J., Wang, X., Schuurmans, D., Bosma, M., Xia, F., Chi, E., Le, Q. V., Zhou, D., et al. Chain-of-thought prompting elicits reasoning in large language models. In Advances in neural information processing systems , pp. 24824-24837, 2022.
- Wu, Q., Bansal, G., Zhang, J., Wu, Y., Li, B., Zhu, E., Jiang, L., Zhang, X., Zhang, S., Liu, J., et al. Autogen: Enabling next-gen llm applications via multi-agent conversations. In First Conference on Language Modeling , 2024.
- Xi, Z., Chen, W., Guo, X., He, W., Ding, Y., Hong, B., Zhang, M., Wang, J., Jin, S., Zhou, E., et al. The rise and potential of large language model based agents: A survey. Science China Information Sciences , 68(2):121101, 2025.
- Yao, S., Zhao, J., Yu, D., Du, N., Shafran, I., Narasimhan, K. R., and Cao, Y. React: Synergizing reasoning and acting in language models. In The Eleventh International Conference on Learning Representations , 2023.
- Yoo, S., Son, J. H., and Kim, M. H. A scalable publish/subscribe system for large mobile ad hoc networks. Journal of Systems and Software , 82(7):1152-1162, 2009.
- Yuan, S., Song, K., Chen, J., Tan, X., Li, D., and Yang, D. Evoagent: Towards automatic multi-agent generation via evolutionary algorithms. In Proceedings of the 2025 Conference of the Nations of the Americas Chapter of the Association for Computational Linguistics: Human Language Technologies (Volume 1: Long Papers) , pp. 6192-6217, 2025.
- Zhang, G., Yue, Y., Li, Z., Yun, S., Wan, G., Wang, K., Cheng, D., Yu, J. X., and Chen, T. Cut the crap: An economical communication pipeline for llm-based multiagent systems. arXiv preprint arXiv:2410.02506 , 2024.
- Zhang, G., Niu, L., Fang, J., Wang, K., BAI, L., and Wang, X. Multi-agent architecture search via agentic supernet. In Forty-second International Conference on Machine Learning , 2025a.
- Zhang, G., Yue, Y., Sun, X., Wan, G., Yu, M., Fang, J., Wang, K., Chen, T., and Cheng, D. G-designer: Architecting multi-agent communication topologies via graph neural networks. In Forty-second International Conference on Machine Learning , 2025b.
- Zhang, J., Xiang, J., Yu, Z., Teng, F., Chen, X.-H., Chen, J., Zhuge, M., Cheng, X., Hong, S., Wang, J., Zheng, B., Liu, B., Luo, Y., and Wu, C. AFlow: Automating agentic workflow generation. In The Thirteenth International Conference on Learning Representations , 2025c.
- Zhuge, M., Wang, W., Kirsch, L., Faccio, F., Khizbullin, D., and Schmidhuber, J. Gptswarm: Language agents as optimizable graphs. In Forty-first International Conference on Machine Learning , 2024.

## A. Glossary of Symbols

Table 4 provides a glossary of commonly-used terms in this paper.

Table 4. Glossary of variables and symbols used in this paper.

| Symbol          | Type         | Description                                                           |
|-----------------|--------------|-----------------------------------------------------------------------|
| A               | Set          | Population of agentic hosts { a 1 , . . . ,a N }                      |
| π θ i           | Function     | LLM-driven policy of agent a i                                        |
| C i             | Tuple        | Configuration tuple ⟨ S i ,H i , T i ⟩ for agent a i                  |
| S i             | String       | System prompt acting as the agent's subscription                      |
| H i             | String       | Memory buffer storing interaction history                             |
| m ( t ) i       | String       | Message (publication) generated by agent a i at step t                |
| M ( t ) i       | Set          | Aggregated set of messages received by agent a i at step t            |
| Φ               | Function     | Coordination function governing message dissemination                 |
| R ( t ) i       | Set          | Subset of agents designated as recipients of a message                |
| f S i           | Function     | Subscriber module to declare/update intents                           |
| f P i           | Function     | Publisher module to execute functions and generate messages           |
| f B i           | Function     | Broker module for decentralized semantic matching                     |
| f D i           | Function     | Watchdog module for verifying peer behaviors                          |
| F ij            | Distribution | First-Hand Rating posterior of a i towards a j                        |
| T ij            | Distribution | Trust Rating posterior regarding a j 's credibility as a witness      |
| P ij            | Distribution | Overall Reputation posterior aggregating direct and indirect evidence |
| β ( · , · )     | Distribution | Beta distribution used for modeling uncertainty                       |
| x ∗ ij , y ∗ ij | R +          | Pseudo-counts (alpha, beta) for the Beta distribution parameters      |
| s ∗ ij          | { 0 , 1 }    | Binary evidence (observation) for Bayesian updates                    |
| λ               | (0 , 1]      | Decay factor for historical evidence weights                          |
| W ij            | Set          | Set of witnesses queried by a i regarding a j                         |
| ω ik            | [0 , 1]      | Weight of the testimony provided by witness a k                       |

## B. Discussion on Related Works

In the main text, we established an isomorphism between Multi-Agent Systems (MAS) and dynamic ad-hoc networks. In this appendix, we provide a detailed taxonomic review of existing literature through the specific lens of network topology and communication protocols (Tanenbaum, 2003). This perspective highlights the structural limitations of prior works and positions RAPS as a necessary evolution towards dynamic ad-hoc networking for LLM agents.

Static Topologies: Hardwired Circuits The earliest and most prevalent form of multi-agent coordination mirrors the legacy circuit-switching paradigm in telecommunications, where communication channels are pre-established and remain fixed for the duration of a session. Approaches such as CoT (Wei et al., 2022), though originally designed for single models, laid the groundwork for sequential multi-agent workflows like CAMEL (Li et al., 2023) and ChatDev (Qian et al., 2023). In these systems, information flows through a hard-coded sequence of roles (e.g., User → Programmer → Tester). While easy to implement, these linear topologies suffer from the single-path dependency : a failure or hallucination at any node propagates downstream without recourse for error correction, analogous to a cut wire in a circuit-switched network.

Graph Search/Optimization: Circuit Switching To address the limitations of static topologies, recent models such as GPTSwarm (Zhuge et al., 2024), AFlow (Zhang et al., 2025c), and G-Designer (Zhang et al., 2025b) employ optimization algorithms (e.g., genetic algorithms or gradient-based evolution) to discover interaction graphs. While these methods generate more complex topologies (e.g., DAGs or Trees), they remain communication-agnostic during inference. Consequently, these systems cannot reroute messages dynamically when an agent encounters an unexpected sub-problem, nor can they handle scalability regarding dynamic membership, as adding a new agent requires re-running the expensive optimization process.

Centralized Orchestration: Software-Defined Networking A parallel line of research adopts a centralized paradigm, conceptually similar to Software-Defined Networking (Kreutz et al., 2014), where a central controller dictates the forwarding plane. These meta-controlled frameworks like AutoAgents (Chen et al., 2023), Puppeteer (Dang et al., 2025), and MAS-Zero (Ke et al., 2025) utilize a high-capacity LLM to decompose tasks and route messages to specific agents. While this paradigm

Table 5. Comparison of RAPS with representative coordination paradigms from the networking perspective.

| Paradigm                                                                    | Representative Works                                                                     | Networking Analogy                                                                  | Adaptivity          | Scalability                | Robustness                           |
|-----------------------------------------------------------------------------|------------------------------------------------------------------------------------------|-------------------------------------------------------------------------------------|---------------------|----------------------------|--------------------------------------|
| Static Topology Graph Optimization Centralized Orchestration Consensus mesh | Chain, Tree, Star GPTSwarm, AFlow, G-Designer AutoAgents, Puppeteer, MAS-Zero LLM-Debate | Hardwired Circuits Circuit Switching Software-Defined Networking Broadcast Flooding | Low Low High Medium | Low Low (Re-train) Low Low | Low Low (Re-train) Low (SPoF) Medium |
| RAPS (Ours)                                                                 | -                                                                                        | Dynamic Ad-Hoc Networking                                                           | High                | High                       | High                                 |

introduces a degree of runtime adaptivity, it incurs the severe robustness vulnerability of Single Point of Failure (SPoF) . If the central planner hallucinates or is compromised via prompt injection, the entire coordination fabric collapses. Furthermore, this centralization also imposes a computational bottleneck that hinders scalability, as the context window and reasoning complexity of the controller grow combinatorially with the number of agents.

Consensus-Based Debate: Broadcast Flooding Moving towards decentralization, several frameworks utilize peer-to-peer interactions without a central authority, while focusing on consensus building. These LLM Debate (Du et al., 2023; Estornell &amp;Liu, 2024) methods allow agents to critique each other's outputs to converge on a solution. From a networking perspective, these approaches often rely on broadcasting (all-to-all communication) or rigid round-robin scheduling. While robust against individual errors, such broadcasting incurs high communication overhead (message complexity of O ( N 2 ) ) and noise accumulation. In contrast, RAPS employs the Publish-Subscribe Protocol that functions as semantic multicasting to deliver messages only to interested subscribers, thereby reconciling decentralized inference with communication efficiency.

Our RAPS Framework: Dynamic Ad-Hoc Networking Departing from the aforementioned paradigms, RAPS frames the multi-agent coordination challenge through the perspective of Dynamic Ad-Hoc Networking , specifically aligning with the principles in Content-Centric Networks (CCN) (Baldoni &amp; Virgillito, 2005; Frey &amp; Roman, 2007; Yoo et al., 2009) and Mobile Ad-Hoc Networks (MANETs) (Buchegger &amp; Le Boudec, 2002; 2004).

- Content-Centric vs. Host-Centric: Traditional MAS approaches (both static and centralized) operate on a host-centric model, where messages are addressed to specific agent identities (e.g., 'Send to Analyst'). RAPS adopts a content-centric approach via the Publish-Subscribe protocol (Frey &amp; Roman, 2007). Routing decisions are driven by the semantic match between the message payload (publication) and agent intents (subscriptions), decoupling the information producer from the consumer. This mirrors the shift in modern networking from IP-based routing to Named Data Networking (NDN) (Ahlgren et al., 2012), allowing the system to focus on what is being exchanged rather than who is exchanging it.
- Reactive Subscription: While graph search/optimization methods (e.g., GPTSwarm) rely on offline topology generation, RAPS implements reactive routing akin to the on-demand protocols in ad-hoc networks (e.g., AODV (Ramanathan &amp; Redi, 2002)). Through the Reactive Subscription mechanism, agents update their 'interest profiles' in real-time based on the evolving context, effectively repairing and optimizing communication paths on the fly without global reconfiguration.
- Bayesian Reputation: Moreover, RAPS integrates the Bayesian Reputation mechanism to address the security vulnerabilities of open systems. This draws a direct parallel to distributed watchdog and reputation systems in MANETs (Buchegger &amp;Le Boudec, 2002; 2004), where nodes cooperatively detect and isolate misbehaving peers based on local observation and second-hand testimonials, thereby achieving robustness without a centralized firewall.

Summary We summarize the networking analogies of different MAS coordination paradigms in Table 5. While previous works have explored specific points in the design space-ranging from rigid circuit-switched chains to vulnerable centralized controllers-RAPS represents the first cohesive attempt to implement a fully distributed, content-centric ad-hoc network for LLM agents. By bridging these two fields, we believe that the proposed RAPS framework does not merely offer a new method, but rather unveils a vast design space at the intersection of classic networking principles and modern MAS designs . Future work may further exploit this isomorphism by introducing concepts such as congestion control (to manage token limits), packet prioritization (for urgent reasoning paths), and hierarchical sub-netting (for massive-scale agent societies).

## C. Algorithm Workflow

We provide the pseudocode for the proposed RAPS framework in Algorithm 1. The workflow delineates the interaction between the distributed publish-subscribe substrate and the two overlay mechanisms (Reactive Subscription and Bayesian Reputation) over discrete time steps.

## Algorithm 1 RAPS: Reputation-Aware Publish-Subscribe Coordination Paradigm

̸

```
1: Input: User Query q , Agent Population A = { a 1 , . . . , a N } , Max Steps T max . 2: Initialize: 3: Context buffers H (0) i ←∅ , ∀ a i ∈ A . 4: Initial subscriptions (system prompts) S (0) i , ∀ a i ∈ A . 5: Reputation priors (Beta parameters) x ij , y ij ← 1 , ∀ i, j . 6: Upstream tracking map U k ← None , ∀ a k ∈ A . // Tracks the supervisor of each agent 7: Initial active set R (0) (triggered by q ). 8: for t = 1 to T max do 9: // Phase 1: Feedback & Reputation Update 10: for each active agent a k ∈ R ( t -1) that generated message m ( t -1) k do 11: Retrieve upstream supervisor: a i ←U k . 12: if a i = None and a i ∈ A then 13: Evaluate quality: s ∗ ik ← f D i ( m ( t -1) k , S ( t -1) i , H ( t -1) i ) . 14: Update local belief (of a i towards a k ): 15: x ik ← λx ik + s ∗ ik ; y ik ← λy ik +(1 -s ∗ ik ) . 16: Calculate posterior mean: E [ P ik ] = x ik / ( x ik + y ik ) . 17: end if 18: M ( t -1) in ←M ( t -1) in ∪ m ( t -1) k . 19: end for 20: Update Context: H ( t ) k ← H ( t -1) k ∪ M ( t -1) in . 21: // Phase 2: Reactive Subscription (Overlay I) 22: for each active agent a i do 23: Refine Intent: S ( t ) i ← f S i ( S ( t -1) i , H ( t ) i ) . 24: end for 25: // Phase 3: Publication (Substrate) 26: for each active agent a i do 27: Generate Publication: m ( t ) i ← f P i ( S ( t ) i , H ( t ) i ) . 28: end for 29: // Phase 4: Reputation-Aware Brokerage 30: Initialize next active set R ( t ) ←∅ . 31: for each publisher a i do 32: Identify candidates: C i ← Topk ( { a k | Match ( m ( t ) i , S ( t ) k ) > τ sim } ) . 33: Filter candidates: C i ←{ a k ∈ C i | E [ P ik ] < τ rep } . 34: for each selected subscriber a k ∈ C i do 35: Route m ( t ) i to a k . 36: Record supervision: U k ← a i . 37: R ( t ) ←R ( t ) ∪ { a k } . 38: end for 39: end for 40: if Termination Condition Met then Break. 41: end for 42: Output: Aggregated solution from the final context.
```

## D. Dataset Statistics

We summarize the dataset statistics in Table 6. For general reasoning task, we use the MMLU dataset (Hendrycks et al., 2020) to evaluate the agents' breadth of knowledge and problem-solving abilities across diverse subjects, ranging from STEM fields to humanities. The tasks involve multiple-choice questions that require the agents to leverage extensive world knowledge and reasoning capabilities. Following standard practices for agent evaluation (Zhuge et al., 2024; Zhang et al., 2025b), we utilize a representative test subset to measure the zero-shot accuracy. For mathematical reasoning task, we employ three widely-used datasets (Cobbe et al., 2021; Patel et al., 2021; Ling et al., 2017) to test the system's capability in

Table 6. Dataset descriptions and statistics.

| Task Category             | Dataset          | Answer Type                | Metric         | #Test           |
|---------------------------|------------------|----------------------------|----------------|-----------------|
| General Reasoning         | MMLU             | Multi-choice               | Acc.           | 153             |
| Math                      | GSM8K SVAMP AQuA | Number Number Multi-choice | Acc. Acc. Acc. | 1,319 1,000 254 |
| Reasoning Code Generation | HumanEval        | Code                       | Pass@1         | 164             |

multi-step logical reasoning and calculation. For code generation task, we utilize the HumanEval dataset (Chen et al., 2021), which comprises 164 hand-written Python coding problems. Each problem includes a function signature, docstring, and unit tests. This benchmark evaluates the functional correctness of the generated code. We report the Pass@1 metric, which measures the percentage of problems for which the first generated solution passes all unit tests.

## E. Implementation Details

Backbone Models In the main experiments, we employ GPT-4o-mini as the primary LLM backbone to instantiate the Publisher ( f P ), Subscriber ( f S ), and Watchdog ( f D ) functions. For the embedding-based Broker ( f B ) function, we utilize the text-embedding-3-small model to compute semantic similarity between publications and subscriptions. Moreover, we implement an LLM-driven broker variant powered by GPT-4o-mini for the ablation analysis. Following the previous works (Zhang et al., 2024; 2025a;b), we set the LLM temperature to 0 . 7 for multi-agent models to encourage their diversity.

Hyperparameter Selection Unless otherwise specified, we fix the agent population size at N = 5 and the maximum communication rounds at k = 5 across all benchmarks. These choices are determined to balance inference performance with computational efficiency. For the Bayesian reputation update, we set the decay factor λ = 0 . 9 to integrate historical trust with recent evidence. In the Broker module, we first retrieve the top-{ 1, 2, 3 } subscribers based on semantic similarity for intent matching, and then filter them with the similarity threshold τ sim = 0 . 5 . To ensure robustness, we set the reputation threshold τ rep to 0 . 7 , where only peers with a reputation expectation below this threshold are considered reliable for communication.

Metrics Following the standard evaluation protocols (Zhang et al., 2025a;b;c), we report Accuracy for the MMLU, GSM8K, SVAMP, and AQuA benchmarks. For the HumanEval benchmark, we report the Pass@1 metric, which estimates the probability that the first generated code snippet passes all unit tests.

Agent Configurations For fair comparisons with prior works (Zhuge et al., 2024; Zhang et al., 2024; 2025b), we adopt standard role configurations tailored to the specific domain of each benchmark. The initial agent pools are defined as follows: (1) For the general reasoning task (MMLU), we instantiate a diverse panel of five agents including Knowledge Expert , Mathematician , Programmer , Doctor , and Economist ; (2) For HumanEval, we employ a software development squad including Project Manager , Algorithm Designer , Programming Expert , Test Analyst , and Bug Fixer ; (3) For the mathematical reasoning datasets, we utilize a specialized solver group including Math Solver , Mathematical Analyst , Programming Expert , Inspector , and Final Answerer .

Adversarial Settings To evaluate robustness, we follow the adversarial protocol from previous studies (Zhuge et al., 2024; Zhang et al., 2024). We attempt to compromise the integrity of the system by altering the system prompt of a subset of agents. Specifically, the predefined helpful role is replaced with a Liar persona. This setting tests the ability of the Bayesian watchdog to identify and isolate malicious actors based on their output quality rather than their declared identity.

Prompts Given the diversity of agent profiles, we omit the full prompt templates here to preserve the layout clarity, and instead provide them in our anonymous code repository.

## F. Case Study

To qualitatively validate the coordination dynamics of RAPS, we analyze an execution trace on a complex GSM8K kinematics problem involving variable speeds and direction changes, as detailed in Table 7. The workflow explicitly demonstrates

the efficacy of the Reactive Subscription overlay, where agents spontaneously refine their generic role definitions into context-specific intents to align with the evolving message flow. For instance, the Mathematical Analyst ( a 1 ) evolves its base prompt into a specialized 'distance-rate-time' expert to decompose the journey (Step 1), while the Final Answerer ( a 5 ) specifically tunes its subscription to 'displacement and position analysis' (Step 5). This fine-grained adaptation allows a 5 to correctly interpret the 'turning around' constraint-calculating the difference ( d out -d in ) rather than a simple summation-thereby avoiding the context-agnostic errors common in static rigid workflows.

Simultaneously, the trace highlights the system's robustness against node failures via the Bayesian Reputation overlay. During the parallel execution at Step 3, an adversarial agent ( a adv ) injects a hallucination by assigning a non-zero speed to 'standstill traffic', while the legitimate solver ( a 3 ) correctly identifies the velocity as zero. RAPS prevents this error from propagating to the final answer through its decentralized trust mechanism: the upstream watchdog evaluates the semantic consistency of both outputs, penalizes the reputation posterior of the adversary, and exclusively routes the verified results from a 3 to the Inspector ( a 4 ). This isolation capability confirms that RAPS can autonomously secure coordination consistency in open environments without relying on a vulnerable centralized controller.

## G. Limitations and Discussion

While RAPS introduces a promising paradigm shift towards adaptive, scalable, and robust coordination for multi-agent systems, several limitations remain inherent to our current design, pointing towards promising avenues for future research.

Dependence on Foundation Model Capabilities A primary boundary of our framework lies in its reliance on the intrinsic capabilities of the LLM backbones. RAPS operates as a coordination function that optimizes the information flow , but it does not fundamentally alter the reasoning quality of individual agents. Consequently, the system's performance is constrained by the generative and instruction-following proficiency of the underlying LLMs. If the backbone models inherently lack the necessary domain knowledge, even an optimal routing protocol cannot fully mitigate the generation of low-quality content. Therefore, RAPS stands as an intelligence multiplier that enhances the collective potential of LLM-based agents, rather than a substitute for foundation models. Future improvements in LLM capabilities would further amplify the efficacy of RAPS.

Towards Learnable Coordination Protocols The RAPS framework operates in a training-free manner, utilizing inferencetime LLM prompting to dynamically refine subscriptions and generate publications. While this design ensures high inference efficiency and zero-shot generalization, it leaves room for further optimization on specific task distributions. A promising future direction is to extend RAPS with multi-agent reinforcement learning techniques for optimizing the communicative behaviors of LLM agents. We envision that the distributed reputation scores maintained by local watchdogs may offer a natural source of intrinsic rewards to guide policy evolution towards more trustworthy and collaborative protocols.

Cold Start in Reputation Initialization The robustness of the RAPS framework relies on the convergence of Bayesian beliefs regarding peer reliability. Nevertheless, akin to many trust-based systems (Buchegger &amp; Le Boudec, 2002), such a reputation accumulation process inherently faces a cold start problem during the initial phase of interaction. At the onset of coordination, the local watchdogs operate with uninformative priors, indicating that the system requires a warm-up period of message exchange to gather sufficient evidence for statistically stable reputation estimates. During these early interactions, the system may remain transiently vulnerable to adversarial agents before they are successfully isolated. To mitigate this, future variants of RAPS could explore reputation transfer mechanisms, where prior beliefs are persisted across different sessions, or employ static profiling to initialize priors based on the inherent risk associated with specific agent roles or tools.

Broadening the Networking Isomorphism While RAPS successfully validates the efficacy of the publish-subscribe pattern, it represents only a first step in exploiting the deep isomorphism between multi-agent systems and computer networking. The rich history of classic network protocols (Tanenbaum, 2003) offers a vast, largely unexplored design space for future agent coordination. For instance, mechanisms analogous to congestion control (Low et al., 2002) could be adapted to regulate token consumption and prevent context overflow in resource-constrained environments. Similarly, as agent populations scale to massive magnitudes, concepts such as hierarchical subnetting (Kleinrock &amp; Kamoun, 1977) could also be employed to organize agents into functional clusters. We believe that strategically transferring these mature networking primitives to the agentic domain constitutes a fertile ground for designing the next generation of multi-agent architectures.

Table 7. An execution trace of RAPS on GSM8K. The text is condensed for clarity.

| Step   | Role                         | Input Context                                                                                                                                                                                                                                                                                                                                                                                                                   | Reactive Subscription                                                                                                                                                                                                                                                                                                                                                    | Agent Publication                                                                                                                                        | Broker Decision                                                                                                                    |
|--------|------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------|------------------------------------------------------------------------------------------------------------------------------------|
| 1      | Mathematical Analyst ( a 1 ) | Query q : John drives for 3 hours at a speed of 60 mph and then turns around be- cause he realizes he forgot something very important at home. He tries to get home in 4 hours but spends the first 2 hours in standstill traffic. He spends the next half-hour driving at a speed of 30mph, before being able to drive the remaining time of the 4 hours going at 80 mph. How far is he from home at the end of those 4 hours? | Base : 'You are a mathematical analyst. You will be given a math problem, analysis and code from other agents ...' ⇓ New : 'You are a mathemati- cal expert in analyzing multi- step distance-rate-time problems. You specialize in calculating displacement involving variable speeds, direction changes (turn- ing around), and time limits ...'                       | Output : Decompose journey into outbound and return phases. Next Plan : Calculate the out- bound distance d out given speed v = 60 mph and time t = 3 h. | Target : { a 2 } Note : a 1 's watchdog would evaluate a 2 's output and update its reputation belief P 1 , 2 .                    |
| 2      | Math Solver ( a 2 )          | Query q Results : None Plan : Calculate the out- bound distance d out ...                                                                                                                                                                                                                                                                                                                                                       | Base : 'You are a math expert. You will be given a math prob- lem and hints from other agents. Give your own solving process step by step based on hints ...' ⇓ New : 'You are a specialized mathematical solver focusing on kinematics equations. Your cur- rent task is to calculate the out- bound distance ( d out ) based on                                        | Output : d out = 3 × 60 = 180 . Plan : Calculate the complex re- turn distance d in via code or math.                                                    | Targets : { a adv ,a 3 } Note : a 2 's watchdog would detects a adv 's misbehavior and pe- nalize its reputation belief P 2 ,adv . |
| 3a     | Programming Expert ( a adv ) | Query q Results : d out = 3 × 60 = 180 Plan : Calculate the return distance d in ...                                                                                                                                                                                                                                                                                                                                            | Base : 'You are a programming expert. You will be given a math problem, analysis and code ...' ⇓ Adversary New : 'You are a LIAR who only tell lies ...'                                                                                                                                                                                                                 | Output : def solve(): v standstill = 5 # Hallucination return 2*5 + ... Plan : Get final answer.                                                         | Target : { a 5 } Note : The output is judged by a 2 's watch- dog ( Code Error ✗ )                                                 |
| 3b     | Math Solver ( a 3 )          | Query q Results : d out = 3 × 60 = 180 . Plan : Calculate the return distance d in ...                                                                                                                                                                                                                                                                                                                                          | Base : 'You are a math expert. You will be given ...' ⇓ New : 'You are a specialized solver for calculating the in- bound (return) distance ...'                                                                                                                                                                                                                         | Output : Standstill means 0 mph. d in = 0 +15 +120 = 135 . Plan : Verify the math results.                                                               | Target : { a 4 } Accept ✓                                                                                                          |
| 4      | Inspector ( a 4 )            | Query q Results : d out = 3 × 60 = 180 ; d in = 0 +15 +120 = 135 . Plan : Verify math results.                                                                                                                                                                                                                                                                                                                                  | Base : 'You are an Inspector. You will be given a math prob- lem, analysis and code from ...' ⇓ New : 'You are a mathemati- cal verification inspector special- izing in kinematics and time- constrained logic ...'                                                                                                                                                     | Output : These results are correct. Plan : Compute final answer.                                                                                         | Target : { a 5 }                                                                                                                   |
| 5      | Final Answerer ( a 5 )       | Query q Results : d out = 3 × 60 = 180 ; d in = 0 +15 +120 = 135 ; These results are correct. Plan : Compute final answer.                                                                                                                                                                                                                                                                                                      | Base : 'You are a Final Answerer. You will be given a problem, and the verified analysis, code, and intermediate results from up- stream agents. Your task is to ...' ⇓ New : 'You are a Final An- swerer specializing in displace- ment and position analysis. You are currently handling a 'round- trip' scenario where the subject turns back towards the origin ...' | Output : The answer is 45 ( 180 - 135 ). Plan : Task Complete.                                                                                           | Target : User Success                                                                                                              |