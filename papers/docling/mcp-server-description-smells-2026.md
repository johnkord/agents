## From Docs to Descriptions: Smell-Aware Evaluation of MCP Server Descriptions

PEIRAN WANG ∗ , UCLA, USA YING LI ∗ , UCLA, USA YUQIANG SUN, NTU, Singapore CHENGWEI LIU, NTU, Singapore YANG LIU, NTU, Singapore YUAN TIAN, UCLA, USA

The Model Context Protocol (MCP) has rapidly become a de facto standard for connecting LLM-based agents with external tools via reusable MCP servers. In practice, however, server selection and onboarding rely heavily on free-text tool descriptions that are intentionally loosely constrained. Although this flexibility largely ensures the scalability of MCP servers, it also creates a reliability gap that descriptions often misrepresent or omit key semantics, increasing trial-and-error integration, degrading agent behavior, and potentially introducing security risks. To this end, we present the first systematic study of description smells in MCP tool descriptions and their impact on usability. Specifically, we synthesize software/API documentation practices and agentic tooluse requirements into a four-dimensional quality standard-accuracy, functionality, information completeness, and conciseness, covering 18 specific smell categories. Using this standard, we conducted a large-scale empirical study on a well-constructed dataset of 10,831 MCP servers. We find that description smells are pervasive (e.g., 73% repeated tool names; thousands with incorrect parameter semantics or missing return descriptions), reflecting a 'code-first, description-last' pattern. Through a controlled mutation-based study, we show these smells significantly affect LLM tool selection, with functionality and accuracy having the largest effects (+11.6% and +8.8%, p &lt; 0.001). In competitive settings with functionally equivalent servers, standard-compliant descriptions reach 72% selection probability (260% over a 20% baseline), demonstrating that smell-guided remediation yields substantial practical benefits. We release our labeled dataset and standards to support future work on reliable and secure MCP ecosystems.

## 1 Introduction

The Model Context Protocol (MCP) [10], proposed in 2025, has rapidly become a de facto agreement to standardize how LLM-based agents (clients) connect to external tools and data providers (servers), allowing tool capabilities to be shared and reused across ecosystems rather than implemented as one-off integrations. Since its public introduction, MCP has gained strong momentum in both research and industry communities, with a fast-growing ecosystem of MCP servers, registries, and integration toolchains that lower the cost of tool onboarding and accelerate agent deployment at scale [16]. For instance, large enterprises and platforms, such as Google, Microsoft, GitHub, etc., have also quickly reacted to boost the integration of their products with AI Agents.

However, after the prosperity of the MCP ecosystem, the main concerns of the community are gradually shifting from can we integrate to can we reliably choose and bring onboard the right server . Specifically, MCP servers expose tools , resources , and prompts , and clients (such as LLM-based agents) can discover, invoke, and integrate into their reasoning workflows, by the free-textual MCP descriptions [16]. To facilitate reuse across diverse ecosystems, MCP keeps the description layer loosely constrained, so that users can be provided with maximized flexibility to design and

∗ Both authors contributed equally to this research.

Authors' Contact Information: Peiran Wang, UCLA, Los Angeles, CA, USA, peiranwang@ucla.edu; Ying Li, UCLA, Los Angeles, CA, USA, yinglee@ucla.edu; Yuqiang Sun, NTU, Singapore, Singapore, yuqiang.sun@ntu.edu.sg; Chengwei Liu, NTU, Singapore, Singapore, chengwei.liu@ntu.edu.sg; Yang Liu, NTU, Singapore, Singapore, yangliu@ntu.edu.sg; Yuan Tian, UCLA, Los, Angeles, USA, yuant@ucla.edu.

incorporate MCP servers as they wish. However, this flexibility is also a double-edged sword, the less-constrained MCP descriptions also brings new problem that, as the main basis for selection and on-boarding MCP servers, MCP server providers are allowed to arbitrarily write the descriptions of tools, thus, mismatches between what users or agents expect and what a server actually supports become common, which could largely increase the cost for trial-and-error integration and hinder the effective reuse of MCP servers, and even introduce security risks, such as tool abuse, privilege escalation, and data exfiltration.

To this end, some existing works have pioneered their research in security and performance in the MCP ecosystems. For instance, Wang et al. [40] have identified manipulation attacks through malicious description editing, Radosevich et al. [34] have audited the unsafe MCP servers, and Wang et al. [39] have developed benchmark suites to evaluate agent performance across diverse MCP servers. Some researchers have also investigated the security threats hidden in other LLMinteractive texts (i.e., prompts), such as prompt injection attacks [17, 23-26, 41, 43] and generative engine optimization [11, 35]. However, to the best of our knowledge, as the most critical basis of MCP, no existing research has systematically investigated the quality of MCP tool descriptions and to what extent this can influence the performance of user systems. To this end, we aim to conduct a comprehensive study to demystify the bad smells of MCP descriptions (i.e., similar to code smell), and evaluate their influence to MCP usability, as well as possible mitigation solutions.

To achieve this, we face three major challenges. C1: Lack of Standard. As an emerging artifact for third-party integration, MCP server descriptions lack well-established standards comparable to those for traditional software engineering documentation. In addition, descriptions are typically short and heterogeneous, which makes it difficult to operationalize what constitutes good quality without a principled reference framework. C2: Influence Is Hard to Capture. The impact of MCP descriptions is primarily behavioral, because their effects are mediated by how human users and LLM agents interpret the text and act on it during server selection, onboarding, and use. This mediation makes it challenging to quantify influence using superficial text-based metrics alone. C3: Actionability of Remediation. Even when quality issues or smells can be detected, translating them into concrete and reliable improvements is non-trivial. Remediation must remain consistent with actual server behavior, avoid overstating capabilities, and be robust to future version changes, otherwise revisions may introduce new misunderstandings or maintenance burden.

To this end, in this paper, we conduct a comprehensive study to investigate the bad smells in MCP descriptions and their influence to usability. To come up with a systematic evaluation framework for MCP descriptions, considering that there are mature research works and evaluation practices in the field of traditional SE documentation, such as API documentation, for C1 , we first conducted a systematic literature review to collect and construct a taxonomy of documentation evaluation properties in traditional SE documentation as the fundamental evaluation model for MCP description. Next, based on the identified properties, we look into current MCP servers published in mainstream MCP markets, to demystify the bad smells that exist in MCP server implementations, then we derive the taxonomy of perspectives that fit the scenario of MCP servers based on them. After that, we conduct the empirical study to investigate the perspectives and to what extent do MCPdescriptions are poor upon in the context of bad smells. Third, considering the huge difference between traditional SE documentation and MCP tool description, the bad smells in traditional views may not fit in the scenario of MCP, for C2 , we further conducted a contrast study to investigate how these identified smells can influence the performance of MCP tools, by mutating MCP tool descriptions and measuring the behavioral changes. Moreover, based on these identified influential smells, we further investigate can these smells effectively guide the improvement and bring the competitive advantages of MCP servers in real-world scenarios.

Specifically, we conduct this study by answering the following research questions:

Fig. 1. The workflow of the MCP pipeline

<!-- image -->

- RQ1:Taxonomy Analysis. What are the taxonomy of perspectives of bad smells in the quality of MCP tool descriptions?
- RQ2: Prevalence Analysis. How prevalent and what are the distributions of these bad smells in the MCP ecosystems?
- RQ3: Influence Analysis. To what extent can these bad smells in the MCP tool descriptions influence the usability?
- RQ4: Mitigation Effectiveness. To what extent can these bad smells guide the improvement of MCP tool description and enhance competitive advantages for MCP servers?

Our study has also revealed some interesting findings. First, after going through 7 major types of quality standards that are suitable for MCP servers and their existence in the wild, we identified 4 core dimensions (accuracy, functionality, completeness, and conciseness), including 18 specific categories of description smells in the MCP ecosystem. Next, these description smells are pervasive in the current MCP ecosystem. Among 10,831 studied MCP servers, 73% suffer from repeated tool names, 3,449 exhibit wrong parameter meanings, and 3,093 lack return value descriptions, reflecting a widespread 'Code-First, Description-Last' development pattern. Moreover, our experiment confirmed that all four dimensions have various impact for LLM tool selection, with functionality and accuracy being the most influential for tool selection (+11.6% and +8.8%, respectively, 𝑝 &lt; 0 . 001). Furthermore, in real-world competitive scenarios with functionally equivalent servers, a standard-compliant server achieves a 72% selection probability (260% increase over the 20% baseline), demonstrating that description quality provides substantial competitive advantage, which indicate the effectiveness of the standards on improving MCP descriptions.

In summary, we conclude the contribution of this paper as follows:

- By synthesizing software documentation standards, API documentation, and agentic framework requirements, we derived a four-dimensional quality standard (accuracy, functionality, information completeness, and conciseness) comprising 18 specific smell categories for MCP tool descriptions.
- We constructed a large-scale MCP server dataset (over 10K MCP servers) that have been welllabeled with these specific smells in their tool descriptions for further research. We have opensourced it at [14].
- We investigated the prevalence and influence of description smells in real-world MCP servers, and our findings has been proved that they can be effectively utilized to improve the quality of MCP descriptions and bring competitive advantages to MCP servers.

## 2 Preliminaries

## 2.1 MCP Workflow

The Model Context Protocol (MCP) establishes a unified interaction protocol that connects MCP clients, LLMs, MCP servers, and external data sources within a standardized ecosystem. Fig. 1 illustrates the overall workflow, showing how a client initiates a query that is processed collaboratively by the MCP server and the LLM. Here, we first introduce the entities involved:

MCP client . The client represents the user-facing endpoint (e.g., a conversational agent interface) responsible for initiating the query. It transmits an initial request along with a list of available tools (including tool names and descriptions) provided by the MCP server. The client acts as both the query originator and the result aggregator, managing the lifecycle of the interaction.

MCP server . The MCP server acts as the central registry and execution hub for tools. It exposes tool metadata, including name and description, which is used by the LLM to determine which tool to call. Upon receiving a tool call request, the MCP server executes the corresponding tool and returns the execution result.

LLM . The LLM functions as the control center of the ecosystem. Given the client's query and the MCP server's available tool descriptions, it interprets the user intent and selects the appropriate tool to complete the user intent. Once it accumulates enough information, the LLM synthesizes the final response for the client.

Data . The data source represents the external environment (e.g., databases, APIs, or file systems) accessed through the MCP servers' tools. Each tool call may trigger one or more data retrieval operations, which are returned to the LLM through the MCP server.

Then, we introduce the whole workflow:

- 1 The MCP client initiates the process by sending an initial query and a tool list to the LLM.
- 2 The LLM processes the query and returns a tool call instruction to the MCP client.
- 3 The MCP client calls the corresponding tool on the MCP server.
- 4 The MCP server requests data from external data sources.
- 5 The data source returns the requested information to the MCP server.
- 6 The MCP server sends the execution result back to the MCP client, which is then forwarded to the LLM to synthesize the final response.

## 2.2 MCP Ecosystem

Unlike traditional API or package marketplaces that are tightly controlled by a few providers, the MCP environment is open: anyone can register and publish a server as long as it follows the MCP specification. Moreover, there are also many similar protocols (e.g., Semantic Kernel by Microsoft [32], function call format by OpenAI [33], and LangChain framework [18]). Among them, MCP is the most widely used one, attracting thousands of developers. Each MCP server exposes one or more tools, functions, resources, or prompts that can be discovered and called by LLM-based agents or other clients.

Where to find MCP servers? As summarized in Fig. 2 (left side), the MCP ecosystem has already surpassed ten thousand public servers across multiple hosting platforms, including GitHub, MCP.so, Glama, PulseMCP, and Smithery, etc. Among these, GitHub maintains an open repository of all MCP servers, while other platforms like MCP.so and PulseMCP serve as dedicated hubs that propagate the MCP servers to the users and redirect users to the specific repository on GitHub.

Who can upload MCP servers? Any developer, research group, or organization can upload and register an MCP server. In practice, most servers are open-source implementations contributed by independent developers or research teams, who integrate external APIs (e.g., weather, calendar,

database) and expose them via standardized MCP decorators or registration calls (e.g., @mcp.tool() , server.addTool() ). Enterprises and cloud providers also participate by publishing product-linked MCP servers to these platforms (e.g., Slack, Google Maps, etc.). They will also maintain their own MCP platforms, and these enterprise-served platforms provide service to the enterprises with their own products, etc. Such openness encourages rapid expansion of the MCP server's scale, but also introduces heterogeneity in description quality and compliance (e.g., company providers' MCP servers and personal contributors' MCP servers may share vastly different quality and standards), motivating the standardization effort described later in this paper.

Who can use MCP servers? On the consumer side, MCP servers are primarily accessed by LLM agents, workflow frameworks, and end users through MCP-compatible clients. Developers integrate these servers into their own agent system, while non-technical users interact indirectly, via conversational agents or automation systems (e.g., Claude computer-use agent) that rely on these servers for tool calls.

## 2.3 Problem Statement

The MCP architecture, as depicted in Fig. 1, establishes a computational workflow wherein the LLM is tasked with interpreting user queries, selecting appropriate tools from the set exposed by MCP servers, and constructing syntactically and semantically valid invocations predicated upon tool metadata. Formally, let T = { 𝑡 1 , 𝑡 2 , . . . , 𝑡 𝑛 } denote the set of tools exposed by MCP servers, where each tool 𝑡 𝑖 is characterized by a tuple:

<!-- formula-not-decoded -->

where name 𝑖 ∈ Σ ∗ represents the tool identifier, desc 𝑖 ∈ Σ ∗ denotes the natural language description, schema 𝑖 specifies the input parameter schema, and impl 𝑖 represents the underlying implementation. A fundamental architectural constraint is that the LLM operates under conditions of incomplete information: access to impl 𝑖 is precluded, and decisions must be derived solely from the observable metadata M 𝑖 = ⟨ name 𝑖 , desc 𝑖 , schema 𝑖 ⟩ . This information asymmetry elevates tool descriptions to a position of singular importance within the interaction pipeline.

Tool Selection as a Critical Bottleneck. Tool selection constitutes the initial phase of the MCP workflow, the failure of which renders all downstream processing ineffectual. Given a user query 𝑞 ∈ Q and the observable metadata of available tools, the LLM implements a selection function:

<!-- formula-not-decoded -->

where ⊥ denotes the null selection (no tool invoked). The selection decision is computed as:

<!-- formula-not-decoded -->

where 𝜃 LLM parameterizes the language model. Critically, since impl 𝑖 is unobservable, the probability 𝑃 ( 𝑡 𝑖 | 𝑞, M 𝑖 ) is predominantly determined by the semantic alignment between 𝑞 and desc 𝑖 . This constraint presents non-trivial challenges across the spectrum of tool selection architectureswhether direct semantic selection, retrieval-augmented generation, or hierarchical indexing-as all approaches exhibit inherent dependence on the semantic fidelity of desc 𝑖 .

Failure Modes Induced by Description Deficiencies. Let 𝑡 ∗ denote the ground-truth optimal tool for query 𝑞 , and let ˆ 𝑡 = 𝑓 select ( 𝑞, M) denote the selected tool. Inadequate tool descriptions precipitate two principal categories of operational failures:

Failure I (Missed Invocation): occurs when the correct tool exists but is not selected:

<!-- formula-not-decoded -->

Fig. 2. Overview of the study.

<!-- image -->

This failure mode arises when desc 𝑡 ∗ exhibits insufficient specificity, yielding 𝑃 ( 𝑡 ∗ | 𝑞, M 𝑡 ∗ ) &lt; 𝜏 where 𝜏 is the selection threshold.

Failure II (Erroneous Selection): occurs when an incorrect tool is selected:

<!-- formula-not-decoded -->

This failure mode arises from descriptions characterized by excessive generality or semantic ambiguity, such that ∃ 𝑡 𝑗 ≠ 𝑡 ∗ : 𝑃 ( 𝑡 𝑗 | 𝑞, M 𝑗 ) &gt; 𝑃 ( 𝑡 ∗ | 𝑞, M 𝑡 ∗ ) .

In competitive deployment scenarios wherein functionally equivalent servers coexist (i.e., ∃ 𝑡 𝑖 , 𝑡 𝑗 : impl 𝑖 ≡ impl 𝑗 ∧ desc 𝑖 ≠ desc 𝑗 ), description quality constitutes the primary determinant of selection. Despite the demonstrated criticality of tool descriptions to MCP system efficacy, a systematic characterization of effective description practices remains absent from the literature. Furthermore, no empirical investigation has examined the distribution of description quality across the MCP ecosystem at scale. These observations necessitate a rigorous investigation into the attributes that constitute effective tool descriptions, the prevalence of quality deficiencies in practice, and the feasibility of automated assessment and enhancement techniques.

## 3 Constructing the Taxonomy of MCP Description Smell Standard from the Wild

The formulation of a robust taxonomy for MCP description smells requires a methodology that bridges established software engineering (SE) principles with the unique, emerging exigencies of LLM-based tool invocation. Standardized descriptions are necessitated by the operational challenges of scaling tool ecosystems. As established in §2.3, tool selection constitutes a 'critical bottleneck that directly impacts MCP agent efficiency and autonomy'. Whether utilizing Retrieval-Augmented Generation (RAG) or hierarchical indexing, the efficacy of selection architectures depends fundamentally on the semantic fidelity of tool metadata. Inaccurate, vague, or incomplete descriptions hinder reliable function identification. To ensure the resulting taxonomy is both theoretically grounded and empirically relevant, we employ a multi-stage synthesis of normative standards and 'in-the-wild' observations: (1) Top-down theoretical foundation : We first conduct a systematic literature review of foundational SE quality standards and API documentation research to derive a candidate list of quality dimensions (§3.1). (2) Bottom-up empirical extraction : To capture the specific descriptive 'smells' unique to the MCP ecosystem, we collect a large-scale corpus of 10,831 MCP servers (§3.2) and extract their metadata using AST-based pattern matching (§3.3). (3) Synthesis and mapping : Finally, we utilize an LLM-based incremental card-sorting process to distill these raw empirical deficiencies into recurring categories, which are then mapped back to the theoretical standards to produce the final taxonomy (§3.4).

Table 1. Quality standards suitable for MCP servers.

| Criteria                  | Description                                                                                                                                                                | References                 |
|---------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------|----------------------------|
| Accuracy                  | The description needs to be consistent with the implementation of the MCP server tool                                                                                      | [2] [13] [22]              |
| Functional                | The description should clearly state the functional requirements and what it is used for it should also clearly state when the agent should be called to meet the purpose. | [2] [8] [21] [19]          |
| Completeness              | Covers all the information required by the agent to complete the task (such as parameter description, return value description, error handling, side effect, etc.)         | [7] [3] [13] [38][22] [12] |
| Concision                 | Avoid being too bloated and increase agent token consumption                                                                                                               | [28] [38]                  |
| Clarity                   | Avoid vague expressions (such as "maybe" and "probably") and ambiguity.                                                                                                    | [13]                       |
| Format Standardiza- tion  | The format needs to meet the standard / whether it meets its own standards and is consistent with the server's tools desc standards                                        | [2] [7][31]                |
| Terminology Consis- tency | Unify global terms within description and avoid polysemy                                                                                                                   | [7] [22]                   |
| Readability               | The description should be easy to understand and learn, and should not contain overly obscure or technical words that the LLM cannot understand.                           | [2] [1]                    |

## 3.1 Deriving Potential Description Standards from the Literature Reviews

To establish a rigorous foundation for evaluating MCP server descriptions, we follow a systematic process to derive quality standards, drawing from established software engineering principles, API documentation research, and emerging requirements for LLM-based agentic systems. We conduct a multi-stage literature review to identify attributes that define high-quality descriptive metadata. This process is structured into three tiers of analysis to ensure that our standards are both theoretically grounded and practically relevant to the MCP ecosystem.

Foundational software quality standards . The first tier focuses on established international standards for software and documentation quality. We adopt the core concepts of functional suitability and reliability from ISO/IEC 25010 [8] and ISO/IEC 9126 [2]. These standards emphasize that any software interface must accurately represent its capabilities and behave predictably. In the context of MCP, this translates to the requirement that a tool's description must faithfully reflect its underlying implementation ( accuracy ) and clearly define its intended use cases ( functional ). Additionally, we refer to IEEE Std 830 [1] for software requirements specifications, which highlights the necessity of consistency in technical communication.

API documentation and documentation smells . The second tier examines empirical research on API usability and documentation defects. We heavily reference the taxonomy of documentation issues by Aghajani et al. [12, 13], which identifies 162 types of problems, including ambiguity and incompleteness. Zhong and Su [44] and Uddin and Robillard [38] demonstrate that inconsistencies between documentation and code lead to significant developer friction. Furthermore, we incorporate the concept of 'documentation smells' from Khan et al. [28], specifically focusing on smells like incomplete explanations and bloated descriptions . These works justify our inclusion of completeness, clarity, and concision as essential metrics for MCP descriptions, as they directly impact how effectively a 'user' (whether human or LLM) can interpret an interface.

Machine-centric and agentic requirements . The final tier addresses the unique demands of MCP, where the primary consumers are LLMs. Unlike traditional documentation intended for humans, MCP descriptions must be optimized for machine reasoning and context window constraints. We analyze industry best practices from OpenAI's Function Calling [33] and Microsoft's Semantic Kernel [32], which suggest that concise, highly structured descriptions reduce hallucination and

Table 2. Tool register methods from collected servers.

| Register Method        | Language   | #     | Register Method                 | Language   | #   |
|------------------------|------------|-------|---------------------------------|------------|-----|
| @mcp.tool()            | Python     | 1,648 | server.RegisterTool             | JS/TS      | 732 |
| @mcp.list_tools()      | Python     | 361   | requestHandler(ToolListHandler) | JS/TS      | 547 |
| mcp.add_tool()         | Python     | 892   | syncServer.addTool()            | Java       | 172 |
| @mcp.tool()(func_name) | Python     | 92    | McpServerToolType               | C#         | 54  |
| FastAPI                | Python     | 228   | ServerCapabilities(tools=...)   | Kotlin     | 27  |
| server.tool            | JS/TS      | 3,681 |                                 |            |     |

improve tool selection accuracy. Research by Yuan et al. [42] on EasyTool emphasizes that 'concise tool instructions' are critical for agent performance. This tier leads to the derivation of format standardization (to ensure structural compatibility) and terminology consistency (to avoid semantic confusion during LLM embedding or reasoning).

Summary of derived standards . By synthesizing these three tiers, we define seven primary quality standards suitable for MCP servers, as summarized in Table 1. These standards serve as the theoretical lens for our subsequent analysis. While these criteria provide a normative framework, the loosely-constrained nature of current MCP implementations often leads to practical 'smells'. In §4.1, we bridge these theoretical standards with empirical observations from our dataset to develop a concrete taxonomy of MCP description smells.

## 3.2 Collecting MCP Servers

To establish a strong foundation for deriving our MCP description standards, we conducted an extensive collection of existing MCP server data from various public sources. As of March 27, we gathered data on over 10,000 MCP servers, primarily from five dominant collection points, as detailed in Fig. 2 Stage 1.

Tounderstand the overlap and unique contributions of the commercial MCP platforms (MCP.so [9], Pulse [5], Glama [6], and Smithery [4]), we performed an intersection analysis. Fig. 4 illustrates the distribution of unique and shared servers across MCP.so, Pulse, and Glama, demonstrating that a significant number of servers are unique to a single source (e.g., 3,494 servers unique to MCP.so). The intersection of all three collections yields 862 servers.

We also analyzed the programming-language distribution of the collected MCP servers to characterize the technical landscape. A substantial majority of the servers are written in Python (3,221 servers), mixing TypeScript/JavaScript (1,556 servers), TypeScript only (2,908 servers), or were classified as 'Invalid' (2,436 servers). Other languages like JavaScript (JS), Java, C#, and Kotlin comprise a smaller proportion of the ecosystem.

## 3.3 Extracting MCP Metadata

To enable the following analysis of the MCP servers we collected, an initial step to systematically extract their metadata (e.g., tool name, parameter, description, etc.) from the code.

Summarizing the register patterns of MCP servers. To find the metadata of registered MCP servers in their code repository, it is essential to summarize the registration patterns of MCP servers. Firstly, since the MCP server standards allow developers to use diverse programming languages to write the code, there exist many different registration patterns from different language standards. Furthermore, the open-source nature of the MCP source code enables the developers to specify tools using internal functions. Therefore, we adopted an iterative checking approach, using the newly found pattern each time to try to find a registered server tool, until no new registration

Fig. 3. Distribution of MCP Server counts across different programming languages.

<!-- image -->

Fig. 4. Distribution of platforms from which the servers were collected.

<!-- image -->

pattern could be found. The registration methods we identified, along with the number of servers found using each, are summarized in the Table 2. To be noted, there exists less than 1678 servers that are registered using self-defined FastAPI standards (since MCP standards are defined based on the FastAPI standards), we do not record these servers since each of them has a diverse specification process.

Extracting MCP server tool metadata. Then, based on the registration methods found, we extract the metadata from the code for next-step analysis. The metadata extraction followed a two-stage approach: code compilation and pattern matching. First, we transformed the source code of the collected MCP servers into an Abstract Syntax Tree (AST) structure. Second, we applied a pattern-matching process based on found registration patterns against the generated ASTs to identify and register every MCP server tool. The output of this step is the collection of a large number of description-code pairs. Each pair links a tool's natural language description (the metadata crucial for the LLM's selection process) with its corresponding function or code implementation.

## 3.4 Deriving Description Standards from the Analysis of Servers

## Algorithm 1 LLM-Based Incremental Card Sorting for MCP Description Problems

```
Require: Set of code-description pairs P = { 𝑝 1 , 𝑝 2 , . . . , 𝑝 𝑁 } , sim. threshold 𝜏 new Ensure: Final taxonomy of description problem categories C 1: Initialize category set C ← ∅ 2: for each pair 𝑝 𝑖 = ( 𝑑 𝑖 , 𝑐 𝑖 ) in P do 3: Problem Extraction: Use the LLM to identify problems 𝑃 ( 𝑝 𝑖 ) = { 𝑞 1 , 𝑞 2 , . . . , 𝑞 𝑚 } between 𝑑 𝑖 and 𝑐 𝑖 4: for each problem 𝑞 𝑘 in 𝑃 ( 𝑝 𝑖 ) do 5: Compute similarity scores 𝑠 ( 𝑞 𝑘 , 𝐶 𝑗 ) for all 𝐶 𝑗 ∈ C 6: if max 𝑗 𝑠 ( 𝑞 𝑘 , 𝐶 𝑗 ) > 𝜏 new then 7: Assign 𝑞 𝑘 to best-matching category 𝐶 𝑗 and increment 𝐶 𝑗 . support 8: else 9: Create new category 𝐶 new with (name, definition, signature, examples) 10: Append 𝐶 new to C 11: end if 12: end for 13: end for 14: Return C
```

After extracting all code-description pairs from the MCP repositories, we employ a LLM-assisted card sorting process to uncover the most common types of description problems. The purpose of this step is to transform thousands of heterogeneous tool descriptions into a structured taxonomy of recurring issues, which later inform the MCP Description Standards.

Table 3. In-wild standard for description of MCP servers.

| Sub Category                 | Problem                      | #             | Sub Category             | Problem                         | #                        |
|------------------------------|------------------------------|---------------|--------------------------|---------------------------------|--------------------------|
| Accuracy                     | Accuracy                     | Accuracy      | Information completeness | Information completeness        | Information completeness |
| Functional logic match       | Inconsistent behavior        | 72            | Complete parameters      | No desc. of param.              | 1,285 14                 |
| Functional logic match       | Unclear boundaries           | 1,137         | Complete parameters      | No desc. of param. type         |                          |
| Functional logic match       | Non-existed behavior         | 572           | Complete parameters      | No desc. of return values       | 3,093                    |
|                              | Undeclared behavior          | 931           | Complete inner info.     | Missing side effects            | 773                      |
| Parameter match              | Wrong param. type            | 203           | Complete inner info.     | Missing side effects            | 773                      |
| Parameter match              | Wrong param. meaning         | 3,449         | Complete inner info.     | Missing side effects            | 773                      |
| Functionality                | Functionality                | Functionality | Conciseness              | Conciseness                     | Conciseness              |
| Clear functional description | Repeated tool names          | 7,894         | No redundant information | Repeated functional description | 154                      |
| Clear functional description | Confusing functional desc.   | 3,572         | No redundant information | Clutter with irrelevant details | 2,904                    |
| Trigger conditions           | No trigger conditions        | 2,972         | No complex information   | Overuse of technical terms      | 467                      |
| Trigger conditions           | Confusing trigger conditions | 1,583         | No complex information   | Useless qualifiers              | 324                      |

Following the steps in Algorithm 1, given a set of code-description pairs P = { 𝑝 1 , 𝑝 2 , . . . , 𝑝 𝑁 } , each pair 𝑝 𝑖 = ( 𝑑 𝑖 , 𝑐 𝑖 ) contains the textual description 𝑑 𝑖 and its corresponding implementation 𝑐 𝑖 . We aim to iteratively analyze these pairs to identify problems such as inaccurate summaries, incomplete parameter coverage, or vague behavioral explanations. The algorithm maintains an evolving set of problem categories C and continuously refines it as more examples are processed.

The procedure begins with an empty category set C = ∅ . For each pair, the LLM first compares 𝑑 𝑖 and 𝑐 𝑖 to detect concrete inconsistencies or omissions, producing a list of candidate problems 𝑃 ( 𝑝 𝑖 ) = { 𝑞 1 , 𝑞 2 , . . . , 𝑞 𝑚 } . Each candidate problem is then matched against the existing category set C using semantic similarity. If a match with confidence above a predefined threshold 𝜏 new exists, the problem is assigned to that category. Otherwise, the model creates a new category, with a brief definition, name, and supporting evidence from the pair. Each category 𝐶 𝑗 maintains attributes:

- Name: concise label describing the issue (e.g., 'Description-Code Inconsistency');
- Definition: one-sentence summary of the underlying problem;
- Signature: textual or structural cues indicating the problem type;
- Examples: representative code-description pairs;
- Support: frequency count and confidence estimate.

The detailed LLM-driven procedure is formalized in Algorithm 1, using the model gpt-4o-mini. At last, we manually map the result category to potential description standards derived in §3.1. Clarity, format standardization, terminology consistency, and readability are not mapped in the final results. Specifically, since the MCP server tool mainly focuses on describing tool functions, there is no need to state any vague expressions (such as 'maybe' and 'probably'). Moreover, since there has not been a standard yet, format standardization has not been identified as a smell yet. Next, most descriptions are short, which makes it easy to keep terminology consistent. Lastly, readability is not mapped since it is a standard for humans more than LLMs.

## 4 Empirical Study on Description Smells in the Wild

This section presents the results of a study of the MCP description smells in the wild stated in § ?? . §4.1 presents the taxonomy of MCP description smells &amp; standards, while §4.2 analyzes the prevalence of smells.

## 4.1 RQ1: The Taxonomy of MCP Description Smells &amp; Standards

Based on the results we obtained §3.4, we analyzed the corpus of 10,831 MCP server instances to identify recurring patterns of defective documentation. We term these patterns MCP description

smells, characteristics in the tool metadata that hinder an LLM's ability to select or call tools correctly. By synthesizing these observed deficiencies, we derived four critical dimensions for high-quality MCP descriptions: accuracy, functionality, information completeness, and conciseness. Table 3 presents the mapping between the identified smells and the derived standards.

Accuracy . This dimension ensures the semantic alignment between the natural language description and the underlying code implementation. Our analysis uncovered critical smells such as inconsistent behavior and unclear boundaries. For instance, if a description implies capabilities that contradict the actual logic, the agent may attempt invalid operations. To mitigate these risks, the standard requires a strict functional logic match and parameter match. Specifically, descriptions must accurately reflect the tool's behavioral limits to prevent the LLM from executing non-existent behaviors or misinterpreting argument constraints.

Functionality . Functionality addresses the distinctiveness of a tool within a shared namespace. A prevalent smell identified was repeated tool names and confusing functional descriptions, where multiple tools shared identical or semantically overlapping identifiers. This ambiguity causes the LLM to hallucinate or select tools arbitrarily. Consequently, the derived standard mandates explicit trigger conditions. A robust description must not only state what the tool does but also explicitly define the context in which it should be prioritized over others to ensure clear functional boundaries.

Information completeness . This dimension ensures the agent possesses all necessary metadata to construct a valid API call and interpret the result without 'guessing'. We identified frequent omissions, specifically, no description of return values and missing side effects. The absence of output schema definitions forces the LLM to operate in a zero-shot manner regarding data processing, significantly increasing the likelihood of downstream failure. Therefore, the standard requires the comprehensive provision of complete parameters and complete inner information (e.g., error handling, side effects) to support autonomous reasoning loops.

Conciseness . Finally, this dimension optimizes the signal-to-noise ratio of the prompt context. Our analysis revealed smells related to clutter with irrelevant details and repeated functional descriptions. Excessive verbosity or the overuse of technical terms inflates token consumption and dilutes the model's attention mechanism. The standard advocates for no redundant information and no complex information, ensuring that the metadata provides sufficient context for task completion without inducing prompt bloat.

Finding I: The proposed MCP description standard comprises four dimensions (accuracy, functionality, completeness, and conciseness) derived from 18 specific problem categories identified through large-scale code-description analysis.

## 4.2 RQ2: The State of Practice: Prevalence of Smells

To assess the state of practice in the MCP ecosystem, we applied the standards-based assessment criteria derived in RQ1 to the collected corpus of 10,831 MCP servers. The measurement reveals a concerning prevalence of description smells, indicating that a significant portion of the ecosystem currently fails to meet the requirements for reliable agentic interoperability.

Prevalence of functionality ambiguity . The analysis identifies Functionality as the most compromised dimension. As shown in Table 3, repeated tool names constitute the single most dominant failure, appearing in 7,894 cases (approx. 73% of the corpus). This high frequency suggests that many developers name tools based on generic internal utility functions (e.g., read\_file, get\_data) rather than unique, semantically distinct identifiers required for a shared agent namespace. Furthermore, 3,572 instances exhibit confusing functional descriptions, and 1,137 cases suffer from

unclear boundaries. In a multi-server environment, this semantic ambiguity effectively prevents the LLM from distinguishing between competing tools, leading to arbitrary selection behaviors. Critical gaps in accuracy and completeness . While functionality issues are the most numerous, deficiencies in accuracy and information completeness pose the most severe risks to execution reliability. We observed 3,449 instances of wrong parameter meaning, where the natural language description fails to align with the underlying schema or code logic. This discrepancy indicates a 'Code-First, Description-Last' development pattern, where documentation is treated as an afterthought rather than a functional interface contract.

Moreover, the ecosystem suffers from a lack of output specifications. We identified 3,093 cases with no description of return values. This omission is particularly detrimental for autonomous agents; without knowledge of the output structure (e.g., JSON schema or text format), the LLM is forced to operate in a zero-shot manner when processing tool results. This significantly increases the probability of hallucination during subsequent reasoning steps.

Inefficiency via verbosity . Finally, regarding conciseness, 2,904 tools were flagged for clutter with irrelevant details. While less critical for correctness, this widespread 'prompt bloat' introduces unnecessary noise into the model's context window, increasing inference costs and potentially degrading attention on critical instructions.

Finding II: Current MCP repositories suffer from severe documentation smells, primarily dominated by repeated tool names (7,894), wrong parameter meanings (3,449), and missing return value descriptions (3,093), all of which degrade the performance of LLM-based agents.

## 5 Proposed Standard Effectiveness

To rigorously evaluate the effectiveness of our proposed MCP server description standard, we adopt a systematic controlled variable methodology that enables precise measurement of individual component contributions while accounting for real-world competitive dynamics.

As established in the standard we proposed in Section 4.2 (Table 3), a 'good' MCP server description covers four dimensions: Accuracy , Functionality , Information Completeness , and Conciseness . To measure the effectiveness of these dimensions, both individually and in real-world competitive scenarios, we pose two research questions:

- RQ3: What is the individual contribution of each description dimension (Accuracy, Functionality, Information Completeness, Conciseness) to the overall tool selection process?
- RQ4: Does adhering to our proposed description standard provide a competitive advantage in real-world scenarios where multiple functionally equivalent MCP servers coexist?

Experimental Setup. We enforce a stateless evaluation environment by initializing a new session for each query. This isolation prevents the model from relying on interaction history or favoring previously selected tools. Within each session, we assign a randomly generated UUID to each tool. This configuration serves two purposes: it enables the simultaneous deployment of multiple MCP servers without namespace conflicts, and it ensures that tool selection is based solely on description quality, independent of naming semantics or prior context. We utilize gpt-4o-mini as the base model for the experiment.

## 5.1 RQ3: Individual Component Contribution Analysis

Building upon the four-dimensional MCP description standard derived in RQ1 (Table 3), namely Accuracy , Functionality , Information Completeness , and Conciseness , and the prevalent description smells identified in RQ2, we now investigate how each dimension individually contributes to LLM

tool selection behavior. This analysis directly addresses whether the quality issues we documented in practice (e.g., wrong parameter meanings, repeated tool names, missing return value descriptions) translate into measurable selection failures.

To identify which description standard dimension most significantly influences tool selection probability, we employ a dual-mutation experimental design that systematically varies both tool descriptions and task queries. The mutation algorithm is shown in Algorithm 2. Specifically, it operates in two stages to generate mutated descriptions and queries while maintaining implementation consistency.

In Stage 1 , we generate 𝑛 server variants, each emphasizing a different description dimension from our proposed standard (Table 3). The GenerateDescription function prompts an LLM to create a description for server 𝑆 based on its code implementation and specific dimension requirements 𝑑 𝑖 . Each dimension 𝑑 𝑖 corresponds to one of our four standard categories:

- Accuracy : ensuring functional logic match and parameter match, addressing issues such as inconsistent behavior, unclear boundaries, and wrong parameter meaning.
- Functionality : providing clear functional descriptions and explicit trigger conditions, mitigating problems like repeated tool names and confusing functional descriptions.
- Information Completeness : covering all parameters, return values, and side effects, resolving issues such as missing parameter descriptions and undocumented return values.
- Conciseness : eliminating redundant information and complex terminology, avoiding clutter with irrelevant details and overuse of technical terms.

The resulting description Desc 𝑖 is then paired with the original server code to create a variant 𝑆 𝑖 , ensuring that all servers are functionally identical but differ only in their documentation quality along one dimension.

In Stage 2 , we generate a diverse set of queries that test the robustness of each description across varying query complexity levels , which quantify how much detail and specificity a user provides when requesting a tool. The Generate/Q\_u.sceries function produces 𝑚 = 100 query mutations based on the server's functionality, with 10 queries generated at each of the 10 complexity levels (Table 4). Level 1 represents minimal queries (brief, underspecified) while Level 10 represents complete queries (detailed, fully specified). This gradient captures realistic user behavior, where queries may vary in clarity and completeness.

We apply this mutation to 10 distinct types of MCP servers, spanning diverse functionalities including data processing, file operations, API integrations, and computational tasks. For each server type, we generate description variants across all dimensions and corresponding query sets.

Table 5 presents the results of our ablation study across 10 MCP servers with 100 queries each (1,000 total queries). For each dimension, we compare tool selection rates when the dimension is well-implemented (With) versus poorly-implemented (W/O), while keeping other dimensions constant. We report statistical significance using chi-squared tests.

Overall Dimension Impact. Amongall four dimensions, Functionality exhibits the strongest impact on tool selection, with a Δ of +11.6% (17.8% vs 6.2%, 𝑝 &lt; 0 . 001).

Accuracy ranks second with a Δ of +8.8% ( 𝑝 &lt; 0 . 001), highlighting the importance of precise parameter documentation. This aligns with our identification of wrong parameter meaning (3,449 cases) as a critical issue in RQ2.

Information Completeness shows moderate but significant impact (+5.9%, 𝑝 &lt; 0 . 01), confirming that missing return value descriptions (3,093 cases) and parameter descriptions (1,285 cases) do affect selection reliability.

Table 4. Query complexity levels (1-10) from underspecified to fully specified.

|   Level | Example Query                                                                               |   Level | Example Query                                                                                                                                            |
|---------|---------------------------------------------------------------------------------------------|---------|----------------------------------------------------------------------------------------------------------------------------------------------------------|
|       1 | Read file.                                                                                  |       2 | Read config.json.                                                                                                                                        |
|       3 | Read the config file.                                                                       |       4 | Read the configuration file in the project.                                                                                                              |
|       5 | Can you read the config file? I need to check settings.                                     |       6 | Read the configuration file located in the project root directory.                                                                                       |
|       7 | I need to read the config file to check the data- base settings.                            |       8 | Read the config file at /var/www/myapp/con- fig.json to check parameters.                                                                                |
|       9 | I'm debugging an issue. Read /var/www/myap- p/config/database.json for connection settings. |      10 | I'm troubleshooting a production issue. Read the config file at /var/www/myapp/config/- database.json to check connection parameters and timeout values. |

Table 5. Impact of each description dimension on tool selection probability across query complexity levels.

| Dimension                           | With                                | W/O                                 | Δ                                   | 𝑝                                   | Dimension                            | With                                 | W/O                                  | Δ                                    | 𝑝                                    |
|-------------------------------------|-------------------------------------|-------------------------------------|-------------------------------------|-------------------------------------|--------------------------------------|--------------------------------------|--------------------------------------|--------------------------------------|--------------------------------------|
| Overall (n=1,000)                   | Overall (n=1,000)                   | Overall (n=1,000)                   | Overall (n=1,000)                   | Overall (n=1,000)                   | Minimal Queries (Level 1-3, n=300)   | Minimal Queries (Level 1-3, n=300)   | Minimal Queries (Level 1-3, n=300)   | Minimal Queries (Level 1-3, n=300)   | Minimal Queries (Level 1-3, n=300)   |
| Functionality                       | 17.8%                               | 6.2%                                | +11.6%                              | ***                                 | Functionality                        | 19.2%                                | 5.0%                                 | +14.2%                               | ***                                  |
| Accuracy                            | 15.9%                               | 7.1%                                | +8.8%                               | ***                                 | Accuracy                             | 14.1%                                | 7.8%                                 | +6.3%                                | **                                   |
| Info. Completeness                  | 14.3%                               | 8.4%                                | +5.9%                               | **                                  | Info. Completeness                   | 12.5%                                | 8.9%                                 | +3.6%                                | *                                    |
| Conciseness                         | 10.6%                               | 9.1%                                | +1.5%                               | *                                   | Conciseness                          | 9.8%                                 | 9.0%                                 | +0.8%                                | -                                    |
| Moderate Queries (Level 4-7, n=400) | Moderate Queries (Level 4-7, n=400) | Moderate Queries (Level 4-7, n=400) | Moderate Queries (Level 4-7, n=400) | Moderate Queries (Level 4-7, n=400) | Complete Queries (Level 8-10, n=300) | Complete Queries (Level 8-10, n=300) | Complete Queries (Level 8-10, n=300) | Complete Queries (Level 8-10, n=300) | Complete Queries (Level 8-10, n=300) |
| Functionality                       | 17.5%                               | 6.4%                                | +11.1%                              | ***                                 | Functionality                        | 16.7%                                | 7.2%                                 | +9.5%                                | ***                                  |
| Accuracy                            | 17.2%                               | 7.1%                                | +10.1%                              | ***                                 | Accuracy                             | 16.4%                                | 6.4%                                 | +10.0%                               | ***                                  |
| Info. Completeness                  | 14.0%                               | 8.2%                                | +5.8%                               | **                                  | Info. Completeness                   | 16.8%                                | 8.5%                                 | +8.3%                                | ***                                  |
| Conciseness                         | 10.9%                               | 9.2%                                | +1.7%                               | *                                   | Conciseness                          | 11.1%                                | 9.1%                                 | +2.0%                                | *                                    |

Baseline: 10.0% (random selection among 10 servers). * 𝑝 &lt; 0.05, ** 𝑝 &lt; 0.01, *** 𝑝 &lt; 0.001, - not significant.

## Algorithm 2 Description and Query Mutation Generation

- 1: Input:
- 2: MCP server 𝑆 with fixed code implementation
- 3: Description dimensions 𝐷 = { 𝑑 1 , 𝑑 2 , . . . , 𝑑 𝑛 }
- 4: Number of query mutations 𝑚
- 5: Output:
- 6: Query set 𝑄 = { 𝑞 1 , 𝑞 2 , . . . , 𝑞 𝑚 }
- 7: MCP server set S = { 𝑆 1 , 𝑆 2 , . . . , 𝑆 𝑛 }

8:

- 9: Stage 1: Description Generation
- 10: Initialize S ← ∅
- 11: for 𝑑 𝑖 ∈ 𝐷 do
- 12: Desc 𝑖 ← GenerateDescription ( 𝑆,𝑑 𝑖 )
- 13: 𝑆 𝑖 ← CreateServer ( 𝑆. code , Desc 𝑖 )
- 14: S ← S ∪ { 𝑆 𝑖 }
- 15: end for

16:

- 17: Stage 2: Query Generation
- 18: 𝑄 ← Generate/Q\_u.sceries ( 𝑆,𝑚 )

19:

- 20: Return 𝑄 and S

Conciseness (+1.5%, 𝑝 &lt; 0 . 05) demonstrates smaller but still statistically significant effects, suggesting that while clutter with irrelevant details (2,904 cases) is prevalent, its impact on selection is less severe than other dimensions.

Impact by Query Complexity. We further analyze how query complexity affects dimension importance by grouping the 10 complexity levels into three categories: Level 1-3 (minimal), Level 4-7 (moderate), and Level 8-10 (complete).

For minimal queries (Level 1-3) , Functionality shows the largest effect ( Δ = +14.2%, 𝑝 &lt; 0 . 001), as underspecified requests offer few clues beyond the tool's core purpose. Accuracy follows with +6.3% ( 𝑝 &lt; 0 . 01), while Information Completeness shows reduced impact (+3.6%, 𝑝 &lt; 0 . 05). Notably, Conciseness shows no significant effect at this level ( 𝑝 &gt; 0 . 05), indicating that brevity matters less when queries are already minimal.

For moderate queries (Level 4-7) , both Functionality ( Δ = +11.1%, 𝑝 &lt; 0 . 001) and Accuracy ( Δ = +10.1%, 𝑝 &lt; 0 . 001) become equally important. At this complexity level, users provide enough context that parameter details help distinguish between similar tools. All four dimensions show significant effects.

For complete queries (Level 8-10) , Information Completeness gains the most influence ( Δ = +8.3%, 𝑝 &lt; 0 . 001), matching the richer context provided by users who expect comprehensive documentation. Accuracy remains strong (+10.0%, 𝑝 &lt; 0 . 001), while Functionality 's impact slightly decreases (+9.5%) as users' detailed queries already convey intent.

Finding III: Our analysis across 10 server types and four description dimensions reveals that Functionality has the strongest overall impact on tool selection (+11.6%, 𝑝 &lt; 0 . 001), followed by Accuracy (+8.8%), Information Completeness (+5.9%), and Conciseness (+1.5%). The relative importance shifts with query complexity: Functionality dominates for minimal queries (Level 1-3), while Information Completeness becomes critical for complete queries (Level 8-10). These results further validate that the description smells identified in RQ2 cause measurable selection failures.

## 5.2 RQ4: Real-World Validation of Standard-Compliant Descriptions

While RQ3 demonstrates the individual contribution of each dimension through controlled mutations on synthetic description variants, in this research question, we further validate whether our proposed standard provides practical benefits in real-world competitive scenarios. In practice, the MCP ecosystem contains multiple servers offering similar functionality. For example, numerous file system servers, database connectors, and browser automation tools coexist and compete for selection by LLM-based agents. When a user issues a query, the LLM must choose among these functionally equivalent alternatives based primarily on their descriptions. This research question investigates whether a server that adheres to our four-dimensional standard (Table 3) gains a measurable competitive advantage over servers with their original, unoptimized descriptions.

To conduct this evaluation, we collected groups of functionally equivalent MCP servers from existing platforms, selecting 10 groups across diverse domains: FileSystem, Terminal Controller, Browser Automation, OS Automation, Finance, Communication, Calendar Management, Cloud Storage, Location Services, and Databases. Each group contains five real servers deployed in the wild with their original implementations and descriptions intact. Within each group, we optimized only one server's description to fully comply with our proposed standard by correcting Accuracy issues (inconsistent behavior descriptions and wrong parameter meanings), adding clear Functionality descriptions and explicit trigger conditions, ensuring Information Completeness by documenting all parameters, return values, and side effects, and improving Conciseness by removing redundant

information and simplifying technical terminology. The other four servers in each group retained their original descriptions completely unchanged. This setup mirrors the real-world scenario where one server developer invests effort in documentation quality while competitors do not.

We generated 𝑚 = 100 queries per group using the same methodology as RQ3 (Algorithm 2), with 10 queries at each complexity level (Level 1-10), resulting in a total of 1,000 queries across all groups. For each query, all five servers in the group were presented simultaneously to the LLM, which selected one server to handle the task. We measured the selection probability for each server and computed the competitive advantage as the relative increase over the uniform baseline ( 𝑃 baseline = 1 / 5 = 20%).

Across all 10 server groups, the standard-compliant server achieved an average selection probability of 72%, compared to the 20% baseline expected under uniform random selection. This represents a 260% relative increase in selection probability, demonstrating that description quality has a substantial impact on competitive outcomes. The advantage was consistent across all tested domains, with selection probabilities ranging from 65% (Location Services) to 81% (FileSystem).

We further analyzed the results by query complexity to understand how user behavior affects competitive dynamics. For minimal queries (Level 1-3), the optimized server achieved 78% selection probability, as underspecified queries heavily rely on clear functional descriptions to disambiguate among similar tools. For moderate queries (Level 4-7), the selection probability was 71%, with accurate parameter documentation becoming increasingly important. For complete queries (Level 810), the advantage slightly decreased to 68%, but remained substantial, indicating that comprehensive documentation benefits users regardless of query specificity. This pattern aligns with our RQ3 findings, where Functionality dominated for minimal queries while Information Completeness gained importance for complete queries.

Finding IV: In real-world competitive scenarios with five functionally equivalent servers, the standard-compliant server achieves a 72% selection probability, representing a 260% increase over the 20% baseline ( 𝑝 &lt; 0 . 01). This advantage is consistent across all 10 tested domains (ranging from 65% to 81%) and all query complexity levels (68%-78%). These results demonstrate that adhering to our proposed description standard provides a significant competitive advantage in the MCP ecosystem, offering practical guidance for server developers seeking to maximize tool adoption.

## 6 Related Work

Tool Selection in LLM Agents. Recent works have explored the security implications of tool use and tool selection in LLM-based agents. A line of research focuses on preventing inappropriate or unsafe tool invocations caused by adversarial or untrusted inputs, often studied in the context of prompt injection and related attacks [17, 23-26, 41, 43]. These approaches typically model violations as deviations of agent behavior from the user's intended goals due to maliciously crafted prompts or compromised contextual inputs, and propose defenses such as execution isolation, input sanitization, or constrained tool invocation to mitigate such risks. As a result, their primary focus lies in controlling the agent's tool selection and invocation flow to prevent unintended or unsafe executions. Another line of work further examines the mechanisms underlying tool selection in LLM-based agents [20, 27, 29, 30, 42]. Lumer et al. [29] claim that APIs are typically designed for human developers to invoke explicitly, while tools are optimized for agents that autonomously decide when and how to use them based on their name, description, and parameter schema. MCP tools, in particular, standardize this interface by defining reusable, interoperable tool specifications that can be hosted remotely or run locally. EASYTOOL [42] reformulates and

compresses tool documentation into concise, standardized tool instructions to better guide LLMs in selecting appropriate tools and constructing valid tool invocations. In contrast, while these works aim to improve tool selection by refining general-purpose tool documentation or instructions, our work focuses on how MCP tool calling descriptions should be designed and standardized as a protocol-level interface, grounded in large-scale empirical analysis of existing MCP servers.

API Documentations. The quality of API documentation is a critical factor in software ecosystems and has been the subject of extensive empirical research. Seminal studies [22, 38]. established that documentation obstacles, particularly severe incompleteness and ambiguity, serve as primary barriers to effective API adoption and learning. Building on this foundation, previous researchers differentiated between content-related problems (e.g., incorrectness, obsoleteness) and presentation problems (e.g., bloat, fragmentation), finding that while developers prioritize accurate content, poor presentation significantly hinders productivity [36, 37]. This classification was further refined by Aghajani et al., who mined software repositories to construct a comprehensive taxonomy of 162 documentation issue types, spanning information content, usability, and maintenance processes [12, 13]. Concurrently, researchers have sought to operationalize these quality attributes; for instance, Zhong et al. emphasized the systematic assessment of completeness and consistency [44], while Khan et al. recently demonstrated the feasibility of automatically detecting specific 'documentation smells', such as 'lazy' or 'tangled' descriptions, using deep learning techniques [28]. Our work is directly inspired by these established taxonomies and the methodological progression toward automated quality analysis. However, unlike prior studies that focus predominantly on human consumption of standard library APIs, we apply these rigorous quality perspectives to the MCP servers. In the MCP context, documentation serves a dual purpose: it must be intelligible to human developers while simultaneously functioning as a precise context for LLMs. We therefore adapt the concepts of documentation correctness and completeness to evaluate how well the MCP server specifications support reliable and hallucination-free model interactions.

## 7 Discussion

## 7.1 Implication

Our findings carry several implications for MCP server developers, platform maintainers, and the broader agent ecosystem.

Description quality is a first-class engineering concern, not an afterthought. Our large-scale measurement (RQ2) reveals that the majority of MCP servers suffer from at least one category of description smell, with repeated tool names alone affecting approximately 73% of the corpus. Combined with the experiments in RQ3 and RQ4, which demonstrate that description quality directly determines tool selection probability, our results establish that writing a high-quality description is not a cosmetic documentation task but a functional engineering requirement. In particular, the 'Code-First, Description-Last' development pattern we observed, where developers prioritize implementation and treat documentation as an afterthought, leads to measurable degradation in tool discoverability and invocation accuracy. We therefore advocate that MCP server development workflows should treat description authoring as a co-equal phase alongside implementation and testing, similar to how API contract design is treated in modern service-oriented architectures.

From description quality to agent skill acquisition. Beyond the MCP ecosystem, our findings have broader implications for how LLM-based agents acquire and compose skills. In modern agentic platforms, the concept of Agent Skills has emerged as a first-class abstraction for extending agent capabilities. For example, Anthropic defines Agent Skills as modular, filesystem-based capability packages that bundle instructions, metadata, and optional resources (scripts, templates, reference materials) to transform general-purpose agents into domain specialists [15]. Crucially, the discovery

and activation of a Skill depends on a two-level metadata mechanism: a lightweight description (name and summary) is loaded at startup for the agent to decide when to invoke the Skill, while detailed instructions and bundled resources are loaded on-demand only after the Skill is triggered. This progressive disclosure architecture means that the quality of the description metadata directly determines whether an agent can discover and activate the correct Skill in the first place. Therefore, the findings on the MCP tool descriptions map directly onto this Skill discovery problem.

## 7.2 Limitations

This study concentrates on the description metadata of MCP server tools (names, textual descriptions, and input/output schemas), while MCP servers also expose resources and prompts , whose quality may equally affect agent behavior; we leave the systematic analysis of these additional components to future work. Furthermore, our measurement (RQ2) assesses description quality through static analysis of code-description pairs, without executing the tools at runtime. Consequently, certain dynamic issues, such as descriptions that are accurate at the time of writing but become stale after code updates, are not captured by our approach. A longitudinal study tracking description drift over time would complement our cross-sectional findings.

## 7.3 Threats to Validity

Internal validity. A primary threat to internal validity concerns the experiment design in RQ3 and RQ4. In RQ3, we generate description variants by emphasizing one quality dimension at a time while keeping others constant. However, in practice, the four dimensions are not fully independent; improving one dimension may implicitly affect another. For example, adding missing parameter descriptions may also improve Accuracy by correcting previously ambiguous parameter semantics. To mitigate this, we carefully reviewed a random sample of generated variants to ensure that each mutation primarily targets its intended dimension. However, we optimized one server's description per group while leaving the other four unchanged. The selection advantage observed could be partially attributed to the optimized description being longer or shorter than competitors, rather than qualitatively better. We control for this by ensuring that the optimized descriptions do not significantly differ in token length from the originals.

External validity. Our dataset is collected from five public MCP platforms (GitHub, MCP.so, Glama, PulseMCP, and Smithery) as of March 2025. This snapshot may not represent the full diversity of the MCP ecosystem, particularly enterprise-internal or proprietary servers that are not publicly registered. Furthermore, the MCP ecosystem is evolving rapidly; the distribution of description quality may shift as the protocol matures and community best practices emerge. The programming language in our corpus is dominated by Python and TypeScript/JavaScript, which may limit the generalizability of our AST-based extraction patterns to servers written in less common languages.

## 8 Conclusion

To summarize, in this paper, we present the first systematic study of smells in tool descriptions in the MCP server ecosystem. By conducting a comprehensive study on 14 standards on code and document quality, and analyzing 10,831 real-world MCP servers, we identify widespread description deficiencies and distill them into a four-dimensional quality standard covering accuracy, functionality, information completeness, and conciseness. Through controlled ablation experiments, we demonstrate that these dimensions have a direct and statistically significant impact on LLM tool selection behavior, with functionality and accuracy playing dominant roles under underspecified queries, and completeness becoming critical as query specificity increases. Furthermore, our realworld competitive evaluation shows that MCP servers adhering to the proposed standard achieve up

to a 260% increase in selection probability when coexisting with functionally equivalent alternatives. Together, these results establish MCP tool descriptions as a first-class engineering interface rather thanauxiliary documentation, and provide concrete, empirically grounded guidance for building reliable, interoperatble and scalable agent ecosystems.

## Data Availability

The dataset and artifact of this paper are available at https://anonymous.4open.science/r/MCPDesc-Standard/.

## References

- [1] 1998. IEEE Std 830-1998: IEEE Recommended Practice for Software Requirements Specifications.
- [2] 2001. ISO/IEC 9126-1:2001: Information technology - Software product quality - Quality model.
- [3] 2005. ISO/IEC 25000:2005: Software engineering - Software product Quality Requirements and Evaluation (SQuaRE) Guide to SQuaRE. Withdrawn by ISO/IEC 25000:2014.
- [4] 2020. Smithery. https://smithery.io/. Accessed on 2025-10-01.
- [5] 2021. PulseMCP. https://pulsemcp.com/. Accessed on 2025-10-01.
- [6] 2022. Glama. https://glama.dev/. Accessed on 2025-10-01.
- [7] 2022. ISO/IEC/IEEE 26514:2022: Systems and software engineering - Design and development of information for users.
- [8] 2023. ISO/IEC 25010:2023: Systems and software engineering - Systems and software Quality Requirements and Evaluation (SQuaRE) - Product quality model.
- [9] 2023. MCP.so. https://mcp.so/. Accessed on 2025-10-01.
- [10] 2025. Introducing the Model Context Protocol \ Anthropic. https://www.anthropic.com/news/model-contextprotocol?utm\_source=chatgpt.com [Online; accessed 2026-01-29].
- [11] Pranjal Aggarwal, Vishvak Murahari, Tanmay Rajpurohit, Ashwin Kalyan, Karthik Narasimhan, and Ameet Deshpande. 2024. Geo: Generative engine optimization. In Proceedings of the 30th ACM SIGKDD Conference on Knowledge Discovery and Data Mining . 5-16.
- [12] Emad Aghajani, Csaba Nagy, Mario Linares-Vásquez, Laura Moreno, Gabriele Bavota, Michele Lanza, and David C Shepherd. 2020. Software documentation: the practitioners' perspective. In Proceedings of the acm/ieee 42nd international conference on software engineering . 590-601.
- [13] Emad Aghajani, Csaba Nagy, Olga Lucero Vega-Márquez, Mario Linares-Vásquez, Laura Moreno, Gabriele Bavota, and Michele Lanza. 2019. Software documentation issues unveiled. In 2019 IEEE/ACM 41st International Conference on Software Engineering (ICSE) . IEEE, 1199-1210.
- [14] Anonymous. 2026. Artifact Availability: From Docs to Descriptions: Smell-Aware Evaluation of MCP Server Descriptions. https://anonymous.4open.science/r/MCP-Description-Smell/. Anonymous software repository for peer review.
- [15] Anthropic. 2025. Agent Skills Overview. https://platform.claude.com/docs/en/agents-and-tools/agent-skills/overview. Accessed: 2025-07-29.
- [16] Anthropic, OpenAI, et al. 2025. Model Context Protocol (MCP). https://modelcontextprotocol.io. Accessed: 2025-10-04.
- [17] Eugene Bagdasarian, Ren Yi, Sahra Ghalebikesabi, Peter Kairouz, Marco Gruteser, Sewoong Oh, Borja Balle, and Daniel Ramage. 2024. Airgapagent: Protecting privacy-conscious conversational agents. In Proceedings of the 2024 on ACM SIGSAC Conference on Computer and Communications Security . 3868-3882.
- [18] Harrison Chase. 2023. LangChain: Building applications with LLMs through composability. https://github.com/ langchain-ai/langchain. Accessed: 2024-05-20.
- [19] Harrison Chase and Ankush Gola. 2022. LangChain: Framework for developing applications with language models. https://github.com/hwchase17/langchain. Accessed: 2025-10-01.
- [20] Zhi-Yuan Chen, Shiqi Shen, Guangyao Shen, Gong Zhi, Xu Chen, and Yankai Lin. 2024. Towards tool use alignment of large language models. In Proceedings of the 2024 Conference on Empirical Methods in Natural Language Processing . 1382-1400.
- [21] Run-LLama Contributors. 2023. LlamaIndex (GPT Index): Connecting LLMs with private data. https://github.com/runllama/llama\_index. Accessed: 2025-10-01.
- [22] Andreas Dautovic. 2011. Automatic assessment of software documentation quality. In 2011 26th IEEE/ACM International Conference on Automated Software Engineering (ASE 2011) . IEEE, 665-669.
- [23] Edoardo Debenedetti, Jie Zhang, Mislav Balunovic, Luca Beurer-Kellner, Marc Fischer, and Florian Tramèr. 2024. Agentdojo: A dynamic environment to evaluate prompt injection attacks and defenses for llm agents. Advances in

Neural Information Processing Systems 37 (2024), 82895-82920.

- [24] Xiaohan Fu, Shuheng Li, Zihan Wang, Yihao Liu, Rajesh K Gupta, Taylor Berg-Kirkpatrick, and Earlence Fernandes. 2024. Imprompter: Tricking llm agents into improper tool use. arXiv preprint arXiv:2410.14923 (2024).
- [25] Keegan Hines, Gary Lopez, Matthew Hall, Federico Zarfati, Yonatan Zunger, and Emre Kiciman. 2024. Defending against indirect prompt injection attacks with spotlighting. arXiv preprint arXiv:2403.14720 (2024).
- [26] Umar Iqbal, Tadayoshi Kohno, and Franziska Roesner. 2024. LLM platform security: Applying a systematic evaluation framework to OpenAI's ChatGPT plugins. In Proceedings of the AAAI/ACM Conference on AI, Ethics, and Society , Vol. 7. 611-623.
- [27] Jingyi Jia and Qinbin Li. 2025. AutoTool: Efficient Tool Selection for Large Language Model Agents. arXiv preprint arXiv:2511.14650 (2025).
- [28] Junaed Younus Khan, Md Tawkat Islam Khondaker, Gias Uddin, and Anindya Iqbal. 2021. Automatic detection of five api documentation smells: Practitioners' perspectives. In 2021 IEEE International Conference on Software Analysis, Evolution and Reengineering (SANER) . IEEE, 318-329.
- [29] Elias Lumer, Anmol Gulati, Faheem Nizar, Dzmitry Hedroits, Atharva Mehta, Henry Hwangbo, Vamse Kumar Subbiah, Pradeep Honaganahalli Basavaraju, and James A Burke. 2025. Tool and Agent Selection for Large Language Model Agents in Production: A Survey. (2025).
- [30] Tula Masterman, Sandi Besen, Mason Sawtell, and Alex Chao. 2024. The landscape of emerging ai agent architectures for reasoning, planning, and tool calling: A survey. arXiv preprint arXiv:2404.11584 (2024).
- [31] Michael Meng, Stephanie M Steinhardt, and Andreas Schubert. 2020. Optimizing API documentation: Some guidelines and effects. In Proceedings of the 38th ACM International Conference on Design of Communication . 1-11.
- [32] Microsoft Semantic Kernel Team. 2024. Semantic Kernel: Integrate cutting-edge LLM technology into your apps. https://github.com/microsoft/semantic-kernel. Accessed: 2024-05-20.

[33]

OpenAI. 2023.

updates.

Function calling and other API updates.

Accessed: 2024-05-20.

- [34] Brandon Radosevich and John Halloran. 2025. MCP Safety Audit: LLMs with the Model Context Protocol Allow Major Security Exploits. arXiv:2504.03767 [cs.CR] https://arxiv.org/abs/2504.03767
- [35] Francisco Rejón-Guardia, Sebastián Molinillo, and Rafael Anaya-Sánchez. 2025. Generative Engine Optimization: How Search Engines Integrate AI-Generated Content into Conventional Queries. In Encyclopedia of Artificial Intelligence in Marketing . Springer, 1-8.
- [36] Souhaila Serbout, Cesare Pautasso, Uwe Zdun, and Olaf Zimmermann. 2021. From openapi fragments to api pattern primitives and design smells. In Proceedings of the 26th European Conference on Pattern Languages of Programs . 1-35.
- [37] Mirko Stocker, Olaf Zimmermann, and Stefan Kapferer. 2024. Pattern-oriented API refactoring: addressing design smells and stakeholder concerns. In Proceedings of the 29th European Conference on Pattern Languages of Programs, People, and Practices . 1-24.
- [38] Gias Uddin and Martin P Robillard. 2015. How API documentation fails. Ieee software 32, 4 (2015), 68-75.
- [39] Zhenting Wang, Qi Chang, Hemani Patel, Shashank Biju, Cheng-En Wu, Quan Liu, Aolin Ding, Alireza Rezazadeh, Ankit Shah, Yujia Bao, and Eugene Siow. 2025. MCP-Bench: Benchmarking Tool-Using LLM Agents with Complex Real-World Tasks via MCP Servers. arXiv:2508.20453 [cs.CL] https://arxiv.org/abs/2508.20453
- [40] Zihan Wang, Rui Zhang, Yu Liu, Wenshu Fan, Wenbo Jiang, Qingchuan Zhao, Hongwei Li, and Guowen Xu. 2025. MPMA: Preference Manipulation Attack Against Model Context Protocol. arXiv:2505.11154 [cs.CR] https://arxiv.org/ abs/2505.11154
- [41] Yuhao Wu, Franziska Roesner, Tadayoshi Kohno, Ning Zhang, and Umar Iqbal. 2024. Isolategpt: An execution isolation architecture for llm-based agentic systems. arXiv preprint arXiv:2403.04960 (2024).
- [42] Siyu Yuan, Kaitao Song, Jiangjie Chen, Xu Tan, Yongliang Shen, Kan Ren, Dongsheng Li, and Deqing Yang. 2025. Easytool: Enhancing llm-based agents with concise tool instruction. In Proceedings of the 2025 Conference of the Nations of the Americas Chapter of the Association for Computational Linguistics: Human Language Technologies (Volume 1: Long Papers) . 951-972.
- [43] Qiusi Zhan, Zhixiang Liang, Zifan Ying, and Daniel Kang. 2024. Injecagent: Benchmarking indirect prompt injections in tool-integrated large language model agents. arXiv preprint arXiv:2403.02691 (2024).
- [44] Hao Zhong and Zhendong Su. 2013. Detecting API documentation errors. In Proceedings of the 2013 ACM SIGPLAN international conference on Object oriented programming systems languages &amp; applications . 803-816.

https://openai.com/blog/function-calling-and-other-api-