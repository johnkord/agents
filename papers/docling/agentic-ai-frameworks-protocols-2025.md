## Agentic AI Frameworks: Architectures, Protocols, and Design Challenges

Hana Derouiche University of Kairouan

SMART Lab, University of Tunis , Tunisia hana.darouiche@gmail.com, 0009-0009-4162-5633

Haithem Mazeni

University of Jandouba , Tunisia haithem.mezni@gmail.com, 0000-0001-9932-8433

Abstract -The emergence of Large Language Models (LLMs) has ushered in a transformative paradigm in artificial intelligence, Agentic AI, where intelligent agents exhibit goal-directed autonomy, contextual reasoning, and dynamic multi-agent coordination. This paper provides a systematic review and comparative analysis of leading Agentic AI frameworks, including CrewAI, LangGraph, AutoGen, Semantic Kernel, Agno, Google ADK, and MetaGPT, evaluating their architectural principles, communication mechanisms, memory management, safety guardrails, and alignment with service-oriented computing paradigms. Furthermore, we identify key limitations, emerging trends, and open challenges in the field. To address the issue of agent communication, we conduct an in-depth analysis of protocols such as the Contract Net Protocol (CNP), Agent-to-Agent (A2A), Agent Network Protocol (ANP), and Agora. Our findings not only establish a foundational taxonomy for Agentic AI systems but also propose future research directions to enhance scalability, robustness, and interoperability. This work serves as a comprehensive reference for researchers and practitioners working to advance the next generation of autonomous AI systems.

Index Terms -Agentic AI, Large Language Models, Agent protocols, Agentic AI-as-a-Service

## I. INTRODUCTION

1 The rapid advancement of Large Language Models (LLMs) has ushered in a new era of intelligent agents, known as Agentic AI, where autonomous systems, referred to as intelligent agents, can reason, communicate, and coordinate to complete complex, long-horizon tasks. This paradigm shift departs from traditional AI and Multi-Agent Systems (MAS) [1] by introducing agents that are not only context-aware but also capable of goaldirected behavior powered by LLM-based cognition.

1 ©2025 IEEE. Personal use of this material is permitted. Permission from IEEE must be obtained for all other uses, in any current or future media, including reprinting/republishing this material for advertising or promotional purposes, creating new collective works, for resale or redistribution to servers or lists, or reuse of any copyrighted component of this work in other works

Zaki Brahmi University of Sousse Riadi Lab, Compus Manouba , Tunisia zakibrahmi@gmail.com, 0000-0002-0432-4817

Agentic AI is increasingly being deployed in domains such as software engineering [2], scientific discovery, business automation, and human-agent collaboration. To support its capabilities, a growing ecosystem of Agentic AI frameworks has emerged (e.g., CrewAI, LangGraph). These frameworks provide architectural foundations and tooling for building, orchestrating, and deploying intelligent agents. Despite the rapid growth of the Agentic AI paradigm, there remains a lack of systematic understanding of how these frameworks differ in their design philosophies, technical components, and practical capabilities. To our knowledge, the existing literature on this topic remains scarce and often focuses on isolated features. For instance, authors in [3] provide a comprehensive review in the context of financial services.

This paper aims to bridge the gap by offering a comprehensive comparative analysis of leading frameworks such as CrewAI, LangGraph, AutoGen, Semantic Kernel, and MetaGPT. Our study is based on an exploration of the architectural features that characterize major Agentic AI frameworks, highlighting their design patterns and operational components. Attention is also given to the communication protocols (e.g., ACP, ANP, A2A, Agora) adopted by these systems. In addition, the paper investigates how different frameworks handle critical aspects such as memory integration and guardrail enforcement. Finally, it reflects on the current limitations and challenges these systems face, while identifying promising directions for future development in Agentic AI. To this end, we address the following research questions:

- RQ1: How have intelligent agents evolved from traditional AI agents to modern LLM-powered agents?
- RQ2: What frameworks are available for developing agentic AI systems, and how do they implement core agent concept, MAS paradigms (negotiation, collaboration, organization), and communication?
- RQ3: How do these frameworks compare in com-

munication, memory, orchestration, modularity, and guardrails? What recent advances exist in agent communication protocols?

- RQ4: To what extent are modern agentic AI frameworks ready for integration into service computing ecosystems?

The remainder of the paper is organized as follows: Section II discusses the foundations of intelligent agents and communication protocols. Section III examines communication protocols in greater detail. Section IV analyzes Agentic AI frameworks with respect to memory, guardrails, and service computing. Section V outlines current limitations and open research directions. Section VI concludes the paper.

## II. INTELLIGENT AGENT

The concept of an 'agent' in artificial intelligence has evolved significantly over the past decades within foundational paradigms of AI, primarily Multi-Agent Systems (MAS) and expert systems [4]. Traditionally, an agent was defined as an autonomous entity capable of perceiving its environment through sensors and acting upon it through effectors to achieve designated goals. This classical definition emphasized autonomy, reactivity, proactivity, and social ability, core principles in early MAS research [1]. However, with the rise of Large Language Models (LLMs) and transformer-based architectures, modern agents exhibit more dynamic and context-aware behaviors. They are no longer confined to predefined environments but instead operate within fluid, often human-centered contexts. These agents not only reason and act but also interact with external data sources, orchestrate tools, and collaborate with other agents in real time, often asynchronously.

Contemporary agent architectures, including ReAct [5], PRACT [6], RAISE [7], and Reflexion [8], are unified by their reliance on LLMs as reasoning engines, orchestrating planning, memory, dialogue, and tool use through iterative loops. For instance, the ReAct architecture combines Reasoning (chain-of-thought) and Acting (tool use) in an iterative loop.

To break it down, we believe that modern agents fundamentally differ from classical agents (e.g., BeliefDesire-Intention (BDI) agents) by leveraging LLMs and advanced technologies as versatile reasoning engines and dynamic tool portfolios. Table I presents a comparison between traditional and modern AI agents.

Given this broad evolution, it is now necessary to rethink and potentially redefine what constitutes an agent. A modern agent may be better defined as: 'An autonomous and collaborative entity, equipped with reasoning and communication capabilities, capable of dynamically interpreting structured contexts, orchestrat- ing tools, and adapting behavior through memory and interaction across distributed systems. '

## III. AGENT COMMUNICATION PROTOCOLS

The rise of LLM-powered autonomous agents has highlighted critical challenges in interoperability, security, and scalability, largely due to fragmented frameworks and ad hoc integrations [9], [10]. Robust agent communication protocols are essential for enabling peer discovery, context sharing, and coordinated action, forming the backbone of modular and resilient Multi-Agent Systems. These protocols offer clear advantages over traditional interaction models. Agent communication protocols have evolved from early semantic standards such as FIPA ACL in the 1980s-1990s, to web-based systems (e.g., SOAP/WSDL) in the 2000s-2010s, culminating in today's LLM-driven protocols (e.g., ACP, ANP) and prospective neuro-symbolic or quantum-secure architectures. Despite their transformative potential, clear and universally adopted standards remain nascent, creating a gap that hinders the scalability and composability of multi-agent ecosystems [11], [12]. Emerging protocols (e.g., MCP, A2A, Agora) aim to bridge this gap through lightweight JSON-RPC schemas for context exchange, performative messaging, and discovery.

Fundamentally, contemporary communication protocols share a unifying principle: ' eliminate the need for manual integration, custom middleware, or deep protocol-specific expertise by providing standardized, intelligent frameworks for seamless interaction between agents, whether in AI-to-AI, agent-to-network, or multiagent systems .' One of the earliest protocols, the Model Context Protocol (MCP) 2 , was initially designed for structured tool calls via JSON-RPC and secure schema validation. Although MCP follows a client-server model, it can support inter-agent delegation where strict hierarchical roles are required. Later, Google's Agent2Agent Protocol (A2A) [13] introduced a more agent-oriented architecture, enabling capabilities such as memory management, goal coordination, task invocation, and capability discovery. A2A formalizes communication through constructs like Agent Cards, Task Objects, and Artifacts (standardized outputs). To support decentralized identity and semantic interoperability, the Agent Network Protocol (ANP) [14] incorporates decentralized identifiers (DIDs) and JSON-LD semantics, organizing communication around a lifecycle (creation, operation, update, termination) [15]. It accommodates both explicitly defined protocols and natural language negotiation using LLMs. Built on similar principles, the Agent Communication Protocol (ACP) 3 , originally started at IBM, allows agents to communicate via RESTful

2 https://modelcontextprotocol.io/introduction, accessed 10-05-2025

3 https://agentcommunicationprotocol.dev/

TABLE I: Traditional AI agents vs. Modern AI agents

| Aspect               | Traditional AI agents                                                                  | Modern agentic AI systems (LLM-based agents)                                                             |
|----------------------|----------------------------------------------------------------------------------------|----------------------------------------------------------------------------------------------------------|
| Definition           | Autonomous entities with fixed sensing/acting loops; limited by static rules or models | Autonomous reasoning systems using LLMs with dynamic behavior, tool orchestration, and context-awareness |
| Autonomy             | Limited autonomy; often dependent on human input or predefined instructions            | High autonomy; capable of independently performing complex and extended tasks                            |
| Goal Management      | Focused on single, static goals or fixed task planning                                 | Capable of managing multiple, evolving, and nested goals adaptively                                      |
| Architecture         | Rule-based or BDI (Belief-Desire-Intention) models; monolithic design                  | Modular architecture centered on LLMs, with components for memory, tools, context injection, and roles   |
| Adaptability         | Suited to controlled, predictable environments; poor generaliza- tion                  | Designed for open, dynamic, and unpredictable environments                                               |
| Decision-Making      | Deterministic or rule-based logic; symbolic reasoning                                  | Context-sensitive, probabilistic reasoning with adaptive planning and self-reflection                    |
| Learning Mecha- nism | Rule-based or supervised learning with limited updates                                 | Self-supervised and reinforcement learning; continual fine-tuning pos- sible                             |
| Context Handling     | Static or manually coded states and rules                                              | Dynamic context injection via agent protocols (e.g., MCP, A2A) and runtime awareness                     |
| Communication        | Message-passing via ACL or KQML                                                        | Real-time, event-driven collaboration; natural language interfaces                                       |
| Tool Use             | Limited or predefined tools and actions                                                | Dynamic tool invocation, chaining, and API calling based on context                                      |
| Memory               | Optional, often hardcoded or task-specific                                             | Integrated memory systems supporting long- and short-term informa- tion retention                        |

TABLE II: Comparison of modern agentic AI protocols

| Feature         | MCP                         | ACP                                         | A2A                            | ANP                                  | Agora                           |
|-----------------|-----------------------------|---------------------------------------------|--------------------------------|--------------------------------------|---------------------------------|
| Message Format  | JSON-RPC                    | JSON-LD                                     | JSON-RPC/HTTP/SSE              | JSON-LD + NLP                        | PD + Natural Language           |
| Semantics       | Custom performatives        | Goal-oriented messages (e.g., goal, action) | Custom performatives           | PD                                   | PD                              |
| Discovery       | Manual                      | Agent metadata (agent.yml) and Registry     | Agent Card                     | Agent description as JSON-LD         | Exchanging natural-language PDs |
| Frameworks      | LangChain, OpenAgents, Agno | AutoGen, LangGraph, CrewAI                  | AutoGen, CrewAI, LangGraph     | AGORA, CrewAI, Semantic Kernel Agent | -                               |
| Transport Layer | HTTP, Stdio, SSE            | HTTP                                        | HTTP, optional SSE             | HTTP with JSON-LD                    | HTTP with PD                    |
| Use Case        | LLM-tool integration        | Cross-agent collaboration                   | Enterprise agent orchestration | Decentralized agent markets          | Multi-agent environments        |

APIs, using structured JSON messages to encode actions, goals, and intents. Its design is transport-agnostic and compatible with Web3 environments, making it suitable for scalable, cross-organizational communication. At a higher level of abstraction, Agora 4 [16] serves as a meta-coordination layer, integrating multiple protocols including MCP, ANP, and ACP. It introduces Protocol Documents (PDs), which are machine-interpretable specifications that guide agents in selecting or constructing communication protocols. Table II presents a comprehensive comparison of the studied protocols based on criteria including discovery, messaging, layering, etc.

## Key Findings

Modern agentic protocols (MCP, ACP, A2A, ANP, Agora) reflect a shift toward service-oriented interoperability, with JSON-LD/PD semantics enabling dynamic discovery and composition. Yet, fragmentation persists, HTTP dominates transport, but semantic heterogeneity (custom performatives versus goal-oriented/PD messages) limits seamless integration. Frameworks like AutoGen bridge domains, but standardized service contracts (akin to WSDL for agents) remain nascent, hindering large-scale agent-as-a-service adoption.

## IV. AGENTIC AI FRAMEWORKS

## A. Comparative overview

Agentic AI frameworks provide foundational infrastructure for developing systems where agents exhibit autonomy, context-awareness, and goal-directed behavior. These agents, powered by LLMs, dynamically interpret tasks, orchestrate tool use, and adapt to realtime environments. In this section, we synthesize major agentic frameworks by classifying them based on shared principles and usage patterns, highlighting how their design choices shape agent behavior and coordination (see Fig. 1).

Several frameworks focus on structured orchestration and multi-agent workflows. AutoGen [17], developed by Microsoft, enables rich multi-agent conversations with shared tools and modular LLM backends. It provides the backbone for collaborative workflows across domains such as coding and automation. Similarly, CrewAI [18] promotes role-based collaboration among agents, emphasizing coordination and delegation for team-based problem-solving. The listing 1 shows an example of crewAI agent.

```
agent = Agent( role="Research Assistant", goal="Summarize recent AI news",
```

Listing 1: Simple CrewAI Agent

<!-- image -->

Fig. 1: Agentic AI design taxonomy

```
backstory="An AI expert who keeps track of the latest in research.", llm=OpenAI(temperature=0.5), tools=[], memory=True )
```

Another framework, MetaGPT [19], follows a comparable philosophy by simulating real-world software engineering teams, where each agent adopts a specialized role (e.g., project manager or developer) to perform structured tasks in a product lifecycle pipeline. For lightweight and transparent agent composition, SmolAgents and PydanticAI 5 provide minimal yet effective solutions. SmolAgents emphasizes simplicity and modularity, supporting prompt chaining and tool use with low overhead. PydanticAI uses the Pydantic library to define agent schemas, enhancing reproducibility and safety, especially for debugging and deployment.

In terms of orchestration abstraction and development ease, the OpenAI Agents SDK provides a highlevel interface that encapsulates tool use, memory, and instruction-following behavior. Other frameworks lean toward graph-based or declarative orchestration. LangGraph [20] introduces a novel graph-based model for sequencing tasks among LLM agents. By supporting compositional flows and stateful operations, it allows for traceable and scalable agent design, particularly in research and analytics contexts. Along similar lines, Semantic Kernel [21] provides enterprise-grade orchestration with fine-grained control over planning, memory, and skill execution, enabling integration with external systems in structured reasoning scenarios. Agno , meanwhile, promotes a declarative and transparent approach to defining agent goals, tools, and reasoning logic, making it a strong candidate for automation workflows requiring explainability and control.

Finally, frameworks like LlamaIndex and Google ADK push the boundaries of data-centric and distributed agent ecosystems. LlamaIndex empowers agents with

5 https://ai.pydantic.dev/, accessed 10-05-2025

capabilities for querying structured and unstructured data for knowledge-intensive applications. Google ADK , still experimental and designed for scalability, allows orchestration of multi-agent workflows, making it suitable for adaptive AI assistants and enterprise automation.

To distill a generic and reusable agent model by identifying common structural patterns, the proposed class diagram in Fig. 2 schematizes a unified class model.

Fig. 2: Unified class model for Agentic AI frameworks

<!-- image -->

## Key Findings

In practice, frameworks share core components. The LLM enables advanced reasoning through prompt-based interactions enhanced by in-context learning (few-shot, one-shot, chainof-thought prompting), allowing agents to perform complex cognitive tasks with minimal supervision; tools (external actions); memory ; and guardrails to ensure safety, reliability, and validation of agent outputs and actions.

## B. Memory in Agentic AI frameworks

Memory is foundational to agentic AI, enabling context-aware, adaptive behavior [22]. Its mechanisms support retention, retrieval, and reasoning across interactions, facilitating multi-turn dialogues, preference adaptation, and knowledge transfer. Memory can be mainly categorized into (1) short-term memory , which allows agents to maintain the immediate conversational or task context, and (2) long-term memory , which, by contrast, captures persistent data across sessions, such as user preferences, task history, or learned knowledge, that agents can revisit later. Some frameworks also implement specialized forms of long-term memory, such as semantic memory [23], which stores and reuses past reasoning paths or decisions; procedural memory , which recalls specific task flows or strategies previously used; and episodic memory [24], which encodes detailed con-

TABLE III: Memory support in Agentic AI frameworks

| Framework       | Memory Approach                                                                    | Short-Term   | Long-Term   | Semantic   | Procedural   | Episodic   |
|-----------------|------------------------------------------------------------------------------------|--------------|-------------|------------|--------------|------------|
| LangGraph       | Stateful graph nodes retain context between agent transitions.                     | ✓            | -           | -          | -            | -          |
| OpenAI SDK      | Session-based memory abstraction (e.g., ConversationBufferMemory ).                | ✓            | -           | -          | -            | -          |
| SmolAgents      | memory is optional and manually injected.                                          | -            | -           | -          | -            | -          |
| CrewAI          | Agent-level memory for dialogue and coordination, with entity/- contextual memory. | ✓            | ✓           | ✓          | -            | ✓          |
| AutoGen         | Shared memory context maintained across structured dialogues.                      | ✓            | ✓           | -          | -            | ✓          |
| Semantic Kernel | Extensible memory modules integrated with planners and skills.                     | ✓            | ✓           | ✓          | ✓            | -          |
| LlamaIndex      | Embedding-based context retrieval from large-scale indexed data.                   | ✓            | ✓           | ✓          | -            | -          |
| PydanticAI      | Schema-first modeling; external memory systems can be attached.                    | -            | -           | -          | -            | -          |
| Google ADK      | Shared memory across agent instances and system modules.                           | ✓            | ✓           | -          | -            | -          |
| Agno            | Declarative memory structure embedded in agent design.                             | ✓            | -           | -          | -            | -          |
| MetaGPT         | Implicit memory through role-based behavioral.                                     | ✓            | ✓           | ✓          | ✓            | -          |

textual snapshots of specific past interactions or experiences, enabling more nuanced and personalized agent behavior over time [25].

Across the surveyed frameworks, memory is implemented in various ways depending on the target use case and design philosophy. For instance, LangGraph integrates memory as part of its graph-based structure, preserving state within and across nodes, thereby enabling agents to follow structured workflows with context retention. OpenAI's SDK supports memory through conversation sessions, maintaining task-specific state implicitly, which simplifies implementation for developers. CrewAI equips the agent with individual memory, which plays a central role in role-specific coordination and delegation. AutoGen supports structured dialogues among agents where memory can be passed, persisted, or modified across roles [17]. Google ADK maintains shared memory for dynamic collaboration and task handovers. In contrast, Agno employs a more declarative memory approach to enhance transparency and inspectability.

Table III provides a comparative overview of memory support across these frameworks, based on their official documentation and observed implementation patterns.

## C. Guardrails in Agentic AI Frameworks

Guardrails ensure AI agents act safely and predictably by validating outputs, enforcing security, and maintaining workflow integrity. Among current frameworks, AutoGen, LangGraph, Agno, and the OpenAI SDK provide the strongest native support. AutoGen includes validators and retry logic; LangGraph enables advanced flow-level checks via node validation; Agno offers an early-stage trust layer; and the OpenAI SDK supports schema validation with developer-defined safeguards. Others like CrewAI, MetaGPT, and Google ADK provide partial support, while LlamaIndex and Semantic Kernel validate only at specific stages. SmolAgents lacks guardrails entirely, prioritizing developer control over safety. Overall, while guardrail capabilities are emerging, most frameworks require external logic or manual setup for robust enforcement. This highlights a need for standardized, modular safety layers in agentic AI development.

## D. Applications of Agentic AI frameworks

Agentic AI frameworks like CrewAI and LangGraph have been applied across domains to coordinate specialized LLM agents. In finance, they support tasks such as risk management, anomaly detection, and strategy development through multi-agent collaboration [26], [27]. CrewAI enables reasoning over historical data for informed decision-making. LangGraph has been used in intelligent transportation for modular traffic management [28], while CrewAI also supports automated travel planning in tourism by enabling agents to analyze cities and plan itineraries collaboratively [29].

Despite these efforts, broader adoption of agentic AI frameworks faces challenges. Key barriers include a lack of architectural transparency and standardization, as most solutions lack reusable, interoperable designs like those found in service-oriented systems. Leading frameworks (e.g., AutoGen, AutoGPT) remain underutilized in domain-specific fields (e.g., finance, healthcare). Additionally, multi-agent coordination protocols are often inadequate, scalability is limited, and standardized APIs for collaboration are urgently needed (see Section IV-E).

## E. Agentic AI from a service computing perspective

This section addresses RQ4: To what extent are agentic AI frameworks ready for integration into servicecomputing ecosystems? We evaluate their maturity in Table IV by analyzing key service-oriented capabilities, such as dynamic discovery, composition, and orchestration, against the requirements of modern service architectures.

Semantic Kernel and Google ADK offer strong support for service composition through skill planners and cloud integration, respectively. However, neither framework embeds full service computing primitives natively. Their readiness depends on integration with external registries and orchestration layers. LangGraph, with its state machine abstraction, also provides robust composition patterns and extensibility hooks for

TABLE IV: Compatibility of Agentic AI frameworks with core service computing functions

| Framework         | Discovery   | Publishing   | Composition   | Key Observations                                                                                                                   |
|-------------------|-------------|--------------|---------------|------------------------------------------------------------------------------------------------------------------------------------|
| CrewAI            | ×           | ×            | ✓             | Role-based agents with task delegation; requires external registry for discovery and publishing.                                   |
| LangGraph         | ✓ a         | ×            | ✓             | State-machine logic allows robust composition; discovery possible via extension hooks.                                             |
| AutoGen           | ×           | ×            | ∼             | Conversational agents can invoke tools sequentially; limited planning logic.                                                       |
| Semantic Ker- nel | Partial a   | Partial b    | ✓             | Supports dynamic composition via planners, but discovery and publishing mechanisms require external implementation or integration. |
| Agno              | ×           | ×            | ×             | Minimalist reasoning layer; requires external logic for composition.                                                               |
| Google ADK        | Partial a   | Partial a    | ✓             | Service discovery and publishing require integration with Google Cloud services such as API Gateway and Service Directory.         |
| MetaGPT           | ×           | ×            | ∼             | Generates orchestrators and workflows in code; lacks runtime execution support.                                                    |

TABLE V: W3C specifications and their adaptation for Agentic AI frameworks

| Spec.            | Role in Agentic AI                                            | Integration benefits                                                                                                           | Managed AI entities                                                                                                              | Current support                                                                                  |
|------------------|---------------------------------------------------------------|--------------------------------------------------------------------------------------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------|--------------------------------------------------------------------------------------------------|
| WSDL             | Describes agent func- tion contracts                          | Enables discoverability of agent capabil- ities and explicit API documentation                                                 | <portType> describes an agent/tool end- point; <operation> names a callable function; <binding> maps to API or model invocation. | CrewAI and OpenAI SDK are limited to JSON schema for functions wrapping and registration.        |
| BPEL             | Orchestrates multi- agent workflows                           | Enables structured planning and execu- tion of agent-based tasks, error handling, and workflow modularity                      | <process> , <sequence> , <invoke> reflect agent invocation sequences and tran- sitions (planner/executor/critic roles).          | Multi-agent workflows in AutoGen [17].                                                           |
| WS-Policy        | Controls agent runtime configurations.                        | Enforcement of runtime constraints across agents and tasks, allowing dy- namic configurability.                                | <Policy> , <All> , <ExactlyOne> model parameter sets (e.g., temperature, max tokens) of agent tools and behaviors.               | Per-agent runtime policy integration in Agno , Per-call parameter control in Ope- nAI SDK .      |
| WS- Security     | Secures inter-agent communications and authenticates actions. | Ensures confidentiality of exchanged prompts, provenance of agent-generated content, integrity of inter-agent commu- nication. | <SecurityToken> , <EncryptedData> , <Signature> protect agent messages and signed prompts.                                       | JWTs- and encryption- based in inter- agent messaging in SMOLAgent .                             |
| WS- Coordination | Manages session con- text, turn-taking, and agent roles       | Coordination of agent sessions, includ- ing turn-taking, role enforcement, and shared context propagation.                     | <CoordinationContext> , <Register> track sessions and dialog flow between named agents.                                          | Agent SOPs with distinct roles in MetaGPT, Agent in CrewAI are defined by role and turn policies |
| WS- Agreement    | Negotiates QoS among agents                                   | Supports performance-aware selection and delegation of agents, by expressing SLA guarantees.                                   | <ServiceDescriptionTerm> , <GuaranteeTerm> express agent expectations and SLAs for selection.                                    | AutoGen planner selects agents by esti- mated criteria, CrewAI priorities influ- ence selection. |

discovery. LangGraph offers deterministic, fault-tolerant orchestration and can support discovery through simple catalog adapters, making it a strong runner-up. By contrast, CrewAI, AutoGen, Agno, and MetaGPT excel at multi-agent planning or code generation but require an auxiliary service registry (e.g., OpenAPI gateway or service mesh) to participate in fully dynamic service ecosystems. Incorporating such registries would elevate these frameworks from task-centric agent platforms to comprehensive service-computing solutions.

To support service-oriented Agentic AI , current frameworks have begun integrating W3C standards (e.g., WSDL, WS-Policy, BPEL), but adoption remains limited (see Table V). JSON-schema function registration in CrewAI and the OpenAI SDK mimics WSDL, and AutoGen reflects BPEL-style orchestration without declarative syntax. WS-Policy and WS-Security principles appear in Agno and SmolAgents via runtime settings and JWTs, though they lack formal policy or security token formats. Coordination logic and SLA-like behavior exist in frameworks like MetaGPT and CrewAI, yet without formal constructs for WS-Coordination or WSAgreement. Overall, W3C-inspired features are emerging, but standardized, interoperable adoption is still lacking.

## V. LIMITATIONS AND CHALLENGES

Despite rapid progress, current agentic AI frameworks exhibit several critical limitations. These limitations span architectural rigidity, dynamic collaboration constraints, safety risks, and lack of interoperability.

Rigid architectures : Most frameworks enforce static agent roles (e.g., planner, executor, coder), which limits adaptability in dynamic or evolving tasks. For instance, in MetaGPT or CrewAI, once an agent is assigned a predefined role, it cannot easily change behavior during execution.

No runtime discovery : Agents in many systems cannot dynamically discover or collaborate with peers during runtime. Instead, all agent interactions must be statically defined, limiting scalability and emergent cooperation. As a solution, we can implement an agent or skill registry , a central directory where agents can publish and query capabilities. This allows new agents to join the system and form collaborations dynamically.

Code safety : Execution of generated code, which is common in MetaGPT and AutoGen, poses severe safety risks. Generated Python code can include file system access, shell commands, or unsafe imports. To ensure secure execution, sandbox environments such as Docker containers with strict capabilities can be employed. Alternatively, execution can be restricted to pre- approved pure functions with no side effects or external dependencies.

Interoperability gaps : Frameworks operate in silos, each using incompatible abstractions for agents, tasks, tools, and memory. For example, CrewAI's task model cannot be directly interpreted by an AutoGen agent, nor can a SmolAgent planner invoke a LangGraph workflow without significant translation. This fragmentation hinders code reuse, tool portability, and seamless system integration. A promising architectural approach is to adopt SOA principles, by wrapping AI agents as services to expose their capabilities via RESTful APIs. This enables basic cross-framework interaction, allowing, for example, a LangGraph planner to invoke a CrewAI coder remotely. However, REST lacks the expressiveness for complex agent interaction. To address this, an emerging direction is the use of communication protocols inspired by FIPA-ACL or modern standards like AutoGen's messaging layer. In future frameworks, combining both RESTful exposure and protocol-level messaging could enable fully interoperable, collaborative agent ecosystems.

## VI. CONCLUSION

This paper reviews and analyzes major agentic AI frameworks, such as CrewAI, LangGraph, AutoGen, and MetaGPT, focusing on architecture, memory, communication, guardrails, and service computing support. While all aim to support LLM-driven applications, their design priorities vary: some emphasize modularity and memory (e.g., Semantic Kernel), while others focus on collaboration (e.g., AutoGen, ADK) or role-based coordination (e.g., CrewAI). Communication protocols are still evolving, with new paradigms like ACP and Agora suggesting the need for more robust agent-toagent and agent-to-human dialogue models.

Despite rapid progress, current agentic AI frameworks face several critical limitations that impede their generalizability, composability, and support for service computing. To further advance this field, key directions include establishing standardized benchmarks for objective comparison and reproducibility, as well as developing universal agent communication protocols to enhance interoperability and scalability across frameworks. Another promising direction is incorporating MAS paradigms, such as negotiation, coordination, and self-organization, into existing frameworks.

## REFERENCES

- [1] J. Ferber and G. Weiss, Multi-agent systems: an introduction to distributed artificial intelligence . Addison-wesley Reading, 1999, vol. 1.
- [2] P. Bornet, J. Wirtz, T. H. Davenport, D. De Cremer, B. Evergreen, P. Fersht, R. Gohel, S. Khiyara, P. Sund, and N. Mullakara, Agentic Artificial Intelligence: Harnessing AI Agents to Reinvent Business, Work and Life . Irreplaceable Publishing, 2025.
- [3] S. Joshi, 'Advancing innovation in financial stability: A comprehensive review of ai agent frameworks, challenges and applications,' World Journal of Advanced Engineering Technology and Sciences , vol. 14, no. 2, pp. 117-126, 2025.
- [4] Z. Ren and C. J. Anumba, 'Multi-agent systems in constructionstate of the art and prospects,' Automation in Construction , vol. 13, no. 3, pp. 421-434, 2004.
- [5] S. Yao, J. Zhao, D. Yu, N. Du, I. Shafran, K. Narasimhan, and Y. Cao, 'React: Synergizing reasoning and acting in language models,' arXiv preprint arXiv:2210.03629 , 2022.
- [6] Z. Liu, W. Yao, J. Zhang, R. Murthy, L. Yang, Z. Liu, T. Lan, M. Zhu, J. Tan, S. Kokane et al. , 'Pract: Optimizing principled reasoning and acting of llm agent,' arXiv preprint arXiv:2410.18528 , 2024.
- [7] N. Liu, L. Chen, X. Tian, W. Zou, K. Chen, and M. Cui, 'From llm to conversational agent: A memory enhanced architecture with fine-tuning of large language models,' arXiv preprint arXiv:2401.02777 , 2024.
- [8] N. Shinn, F. Cassano, A. Gopinath, K. Narasimhan, and S. Yao, 'Reflexion: Language agents with verbal reinforcement learning,' Advances in Neural Information Processing Systems , vol. 36, pp. 8634-8652, 2023.
- [9] L. Wang, C. Ma, X. Feng, Z. Zhang, H. Yang, J. Zhang, Z. Chen, J. Tang, X. Chen, Y. Lin et al. , 'A survey on large language model based autonomous agents,' Frontiers of Computer Science , vol. 18, no. 6, p. 186345, 2024.
- [10] Y. Yang, H. Chai, Y. Song, S. Qi, M. Wen, N. Li, J. Liao, H. Hu, J. Lin, G. Chang et al. , 'A survey of ai agent protocols,' arXiv preprint arXiv:2504.16736 , 2025.
- [11] S. P. Yadav, D. P. Mahato, and N. T. D. Linh, Distributed artificial intelligence: A modern approach . CRC Press, 2020.
- [12] P. P. Ray, 'A survey on model context protocol: Architecture, state-of-the-art, challenges and future directions,' Authorea Preprints , 2025.
- [13] G. Research, 'A2a: Agent-to-agent protocol,' https://github.com/ google/A2A, 2025, accessed: 2025-04-21.
- [14] Agent Network Protocol Contributors, 'Agent network protocol official website,' https://agent-network-protocol.com/, 2024, accessed: 30-4-2025.
- [15] A. Ehtesham, A. Singh, G. K. Gupta, and S. Kumar, 'A survey of agent interoperability protocols: Model context protocol (mcp), agent communication protocol (acp), agent-to-agent protocol (a2a), and agent network protocol (anp),' arXiv preprint arXiv:2505.02279 , 2025.
- [16] S. Marro, E. La Malfa, J. Wright, G. Li, N. Shadbolt, M. Wooldridge, and P. Torr, 'A scalable communication protocol for networks of large language models,' arXiv preprint arXiv:2410.11905 , 2024.
- [17] Q. Wu, G. Bansal, J. Zhang, Y. Wu, S. Zhang, E. Zhu, B. Li, L. Jiang, X. Zhang, and C. Wang, 'Autogen: Enabling next-gen llm applications via multi-agent conversation framework,' arXiv preprint arXiv:2308.08155 , 2023.
- [18] Z. Duan and J. Wang, 'Exploration of llm multi-agent application implementation based on langgraph+ crewai,' arXiv preprint arXiv:2411.18241 , 2024.
- [19] S. Hong, X. Zheng, J. Chen, Y. Cheng, J. Wang, C. Zhang, Z. Wang, S. K. S. Yau, Z. Lin, L. Zhou et al. , 'Metagpt: Meta programming for multi-agent collaborative framework,' arXiv preprint arXiv:2308.00352 , vol. 3, no. 4, p. 6, 2023.
- [20] J. Wang and Z. Duan, 'Agent ai with langgraph: A modular framework for enhancing machine translation using large language models,' arXiv preprint arXiv:2412.03801 , 2024.
- [21] J. Soh and P. Singh, 'Semantic kernel, plugins, and function calling,' in Data Science Solutions on Azure: The Rise of Generative AI and Applied AI . Springer, 2024, pp. 191-221.
- [22] J. Guo, N. Li, J. Qi, H. Yang, R. Li, Y. Feng, S. Zhang, and M. Xu, 'Empowering working memory for large language model agents,' arXiv preprint arXiv:2312.17259 , 2023.
- [23] G. Sarthou, A. Clodic, and R. Alami, 'Ontologenius: A longterm semantic memory for robotic agents,' in 2019 28th IEEE
24. International Conference on Robot and Human Interactive Communication (RO-MAN) . IEEE, 2019, pp. 1-8.
- [24] C. DeChant, 'Episodic memory in ai agents poses risks that should be studied and mitigated,' arXiv preprint arXiv:2501.11739 , 2025.
- [25] A. M. Nuxoll and J. E. Laird, 'Enhancing intelligent agents with episodic memory,' Cognitive Systems Research , vol. 17, pp. 3448, 2012.
- [26] S. Joshi, 'A comprehensive survey of ai agent frameworks and their applications in financial services,' Available at SSRN 5252182 , 2025.
- [27] I. Okpala, A. Golgoon, and A. R. Kannan, 'Agentic ai systems applied to tasks in financial services: Modeling and model risk management crews,' arXiv preprint arXiv:2502.05439 , 2025.
- [28] H. Chen and Y. Ding, 'Implementing traffic agent based on langgraph,' in ITSSC 2024 , vol. 13422. SPIE, pp. 582-587.
- [29] A. Singh, R. Madhogaria, A. Misra, and E. Elakiya, 'Automated travel planning via multi-agent systems and real-time intelligence,' Available at SSRN 5089025 , 2024.