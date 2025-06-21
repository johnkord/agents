## Toward Formalizing LLM-Based Agent Designs through Structural Context Modeling and Semantic Dynamics Analysis

Haoyu Jia 1* , Kento Kawaharazuka 1 and Kei Okada 1

1 Graduate School of Information Science and Technology, The University of Tokyo, Japan.

*Corresponding author(s). E-mail(s): jia@jsk.t.u-tokyo.ac.jp;

## Abstract

Current research on large language model (LLM) agents is fragmented: discussions of conceptual frameworks and methodological principles are frequently intertwined with low-level implementation details, causing both readers and authors to lose track amid a proliferation of superficially distinct concepts. We argue that this fragmentation largely stems from the absence of an analyzable, self-consistent formal model that enables implementation-independent characterization and comparison of LLM agents. To address this gap, we propose the Structural Context Model , a formal model for analyzing and comparing LLM agents from the perspective of context structure. Building upon this foundation, we introduce two complementary components that together span the full lifecycle of LLM agent research and development: (1) a declarative implementation framework; and (2) a sustainable agent engineering workflow, Semantic Dynamics Analysis . The proposed workflow provides principled insights into agent mechanisms and supports rapid, systematic design iteration. We demonstrate the effectiveness of the complete framework on dynamic variants of the monkey-banana problem, where agents engineered using our approach achieve up to a 32 percentage points improvement in success rate on the most challenging setting.

Keywords: Agents, Context Engineering, Large Language Models

Fig. 1 : We present a composable, explainable, and sustainable framework for LLM agent research and development. Starting from the Structural Context Model , agents are analyzed and compared at a fundamental level, enabling Semantic Dynamics Analysis to discover reusable context patterns. These patterns are then recomposed to rapidly iterate agent designs, which are systematically evaluated on controlled robotic planning benchmarks, forming a closed-loop workflow for principled agent improvement.

<!-- image -->

## 1 Introduction

In recent years, LLM-based agents have become a prominent research topic in robot task planning, primarily because they are not constrained by the limitations faced by classic symbolic planners such as PDDL [1] and TLPLAN [2]: namely static environments, deterministic action outcomes, and complete prior knowledge. Thus, LLM-powered task agents can achieve relatively robust performance under uncertainty. As a result, a large body of research on LLM-powered agents has emerged. These agents are largely built upon similar loop-based real-time architectures, typically following the ReAct [3] paradigm, as shown in Figure 2.

However, behind this flourishing diversity of research lies a profound form of confusion. When we implemented the well-known ReAct agent and another taskdecomposition agent, MLDT [4], within our agent development framework, we were surprised to find that, despite the stark differences in their descriptions at the paper level, their implementation code differed only marginally. Moreover, when the implementation-specific discussions in the MLDT work are set aside, its essential distinction from ReAct primarily reduces to an innovation at the prompt level.

Fig. 2 : The looping architecture of reason-act agents consists of three stages: (1) the system observes the environment state and formats the observations as requests to the LLM; (2) the LLM reasons about the next action based on the current environment state, the LLM context, and the relevant knowledge; (3) the system executes the selected action and waits for the subsequent observations.

<!-- image -->

We had previously assumed that these two agents exhibited fundamentally significant differences, an assumption that this finding effectively overturned. This observation immediately prompted a critical question: given the large number of agents proposed in the literature, might many of them exhibit no substantial differences once engineering details are abstracted away?

Following this line of inquiry, we conducted a preliminary investigation and found that this phenomenon is not an isolated coincidence but rather a widespread pattern, as intuitively shown in Figure 3. Current research on LLM-based agents exhibits a considerable degree of disorder. On the one hand, a large number of overlapping concepts coexist in the literature, placing unnecessary cognitive burdens on both authors and readers. On the other hand, implementation details are tightly coupled with methodological descriptions, obscuring essential yet incremental innovations behind the appearance of seemingly novel contributions.

We perform this classification based on the modification level of the components introduced relative to ReAct , as well as the impact of these additions on the overall action trajectories of the agent. For example, if a work can be realized solely by introducing a new prompt on top of ReAct , its contribution is categorized as promptlevel. This classification is NOT intended to serve as a definitive judgment of the contributions of these works, but rather as an analytical lens to examine whether inaccurate categorization is prevalent in current research on LLM agents. Our findings indicate that such inaccurate positioning is indeed widespread.

We anticipate that some readers may hold strong objections to this classification. However, such reactions further support our claim that the positioning of innovations in the current literature remains fragmented, underscoring the need for a shared understanding within the research community.

Fig. 3 : Disorder in current research on LLM-powered agents. Left: An overabundance of overlapping concepts is currently in use, unnecessarily increasing the cognitive burden on both authors and readers. Right: The tight coupling between engineering details and methodological descriptions obscures the essence of these innovations, leading to inaccurate positioning of the corresponding research. Representative works mentioned here include: ReAct [3], Recursive-LM [5], LLM-PDDL [6], Retroformer [7], Reflexion [8], ToolMaker [9], ToT [10], MLDT [4], InnerMonologue [11], AIOS [12], MemGPT [13].

<!-- image -->

Based on extensive experience in the development of LLM-powered robotic agents, we have gained a direct understanding of the underlying causes of this disorder. This situation does not stem from subjective factors on the part of researchers, but rather from the exceptionally high engineering complexity of current LLM-based agents.

At present, developers have also not yet reached a consensus on engineering practices in agent development, in contrast to the maturity observed in traditional software engineering. As a result, discussions of agent design in the literature inevitably intertwine with implementation-specific considerations. Consequently, the engineering complexity of agent systems leaks into and contaminates academic discourse, mystifying the most essential innovations, and increasing the difficulty of meaningful comparison across different agent designs. This situation has far-reaching consequences for the agent research community. As a feedback effect, the disorder in academic descriptions further causes innovations in agent research to be reused largely at a conceptual or inspirational level, rather than in a concrete and systematic manner. This, in turn, hinders the effective transfer of research outcomes into development practice, forming a negative feedback loop.

In light of this pressing need, this work seeks to draw the community's attention to these issues and to provide an initial attempt to address them.

This paper is a '4-in-1' work, including a model, a methodology, a simulator for benchmarking, and agent designs; each of them can be used independently. In Section 2, we introduce a formal model for describing, analyzing, and comparing agent designs from the perspective of context structure. In Section 3, building upon this formal model, we present a methodology for analyzing agent structures and extracting reusable context patterns. In Section 4, we propose a benchmark for the comprehensive evaluation of task agents, demonstrate how the proposed methodology can be used to study and improve agents, and introduce new agent designs. While each

component is independently applicable, their combination forms an efficient and sustainable closed-loop workflow for agent research and development, in which analysis, pattern discovery, agent design, and benchmarking mutually reinforce each other, as illustrated in Figure 1.

The feasibility of these theoretical methods is enabled by substantial engineering efforts, through which we implemented an automated interoperability layer between LLMs and autonomous systems, as well as a framework built upon the Structural Context Model that significantly improves the reusability of agent components and thereby enhances development efficiency. This paper does not elaborate on these engineering practices; all source code has been made publicly available.

## 2 Structural Context Model for Agent Description

In this section, we introduce a theoretical model consisting of a set of symbols and related theories that represent LLM contexts as sequences of pattern functions. We further elaborate on how to use these symbols to model and explain current engineering practices such as retrieval augmented generation and multi-agent collaboration, to demonstrate its ability to unify these concepts. Finally, we briefly introduce our programming framework that implements this theoretical model.

## 2.1 Relationships with Automata Theory &amp; Compositional Semantics

Historically, automata theory and the Turing machine model have served as foundational formalisms for reasoning about software systems. In a similar vein, it is natural to introduce a formal language, analogous to those used in automata theory, to describe and reason about agent designs in a systematic and implementation-independent manner.

However, LLM-based agents exhibit substantial behavioral differences from the Turing machine model. First, an LLM does not operate over an explicitly defined and closed set of action symbols. Second, unlike a Turing machine that reads and executes one symbol at a time, an LLM processes and acts upon the entire context as a whole at each inference step. In summary, LLM does not treat the context as a deterministic sequence of instructions; instead, it interprets the semantic meaning of the entire context as a single holistic instruction.

To formally model the 'tape of instructions' of an LLM agent, it is necessary to leverage concepts and insights from compositional semantics. According to compositional semantics, the semantic interpretation of an expression is determined by the semantics of its components together with their mode of composition [14].

Although compositional semantics has been subject to extensive criticism and debate, these concerns do not preclude its use as a modeling framework for the semantics of context. This is because: (1) Agent behaviors are not strictly determined by the exact semantics of the context; rather, a controlled degree of semantic loss is acceptable in practice. (2) Agent development primarily focuses on discovering prompts that induce specific behaviors, or on identifying the functional roles of particular prompts.

Consequently, prompts with higher contextuality can always be replaced by alternative prompts that preserve the relevant semantics while exhibiting lower contextuality.

In summary, by viewing an agent as an automaton that executes the semantics of its context, and by modeling contextual semantics through compositional semantics, we establish the Structural Context Model, which describes and analyzes agents from the perspective of context structure.

## 2.2 Proposal: Structural Context Model

## 2.2.1 Symbol Definitions

In this model, a context is modeled as a sequence of instantiated context patterns; a context pattern is a function that returns context items; and a context item is text or multimodal content with a specific authority level.

Context Item A context item is the minimal constituent unit of a context. It typically corresponds to a fragment of text, but may also represent multimodal content such as images. Each context item is associated with a specific role, which indicates the author of this context item; the default role is 'user'. We Denote the space of all context items by Ω; a context item is usually represented by a lowercase Greek letter, for example, α ∈ Ω.

- The space Ω is closed under concatenation ' · ', and a context item can be defined with other context items: γ = α · β . The concatenation operator ' · ' can be omitted, as γ = αβ .
- A text fragment is a context item: α = ('Why didn't this code work?') and β = ('It works on my machine' , Agent).
- The symbol ε is used to denote an empty context item.
- The repetition of a context item can be denoted as superscripts. For example, α 3 = α · α · α
- The symbol ' . . . ' represents any context item(s). This symbol is typically used when the expression has no requirements or guaranties for the context items.
- The production operator is used to represent the concatenation of context items. For example, ∏ n i =1 α i = α 1 α 1 . . . α n . Unlike in mathematics, the order of expanded elements of the product operator matters because the concatenation of context items is not guaranteed to satisfy the commutative law.

Context Pattern A context pattern is a function that returns context items. In this model, context patterns are the central objects of analysis. They are denoted in uppercase Latin letters and may have parameters and internal states denoted in lowercase Latin letters.

- A context pattern can be defined with another context pattern. For example, B ( a, b | d ) = α · d · A ( a, b )
- For example, a context pattern A with parameters a, b and an internal state c is defined as A ( a, b | c ) = α · a · c · β .

- A context pattern instantiated with concrete parameters is a context item. For example, for a pattern C ( a, b ) = a · αβ · b , its instantiation C ( 1 , γ ) = '1' · αβγ is a concatenation of context items.
- If both the input and the output of a pattern are sequences of context items, the pattern is referred to as a transform pattern, which is a special class of context patterns. Transforms can be represented in a simplified notation. For example, a transform pattern D that replaces only the leading Context Items of a sequence can be written as α... D = ⇒ βγ . . . .
- For parameter-free patterns, the empty item ε can be used as a parameter to explicitly distinguish a parameter-free pattern itself from its instantiation. For example, this convention distinguishes the instantiation A ( ε ) from the parameter-free pattern function A ( ).
- Session A session is a tuple of input items sent to the agent and the output items received from the agent, denoted as S = ( I , O ). The sequence of input items of the i -th session S i is denoted as I i ; correspondingly, the sequence of output items is denoted as O i . According to the concatenation rule of Context Items, both the input and the output of a session are context items. Therefore, a session can be regarded as an ordered pair of context items.
- Activity An activity is an ordered list of sessions that are performed for specific purposes, denoted A := [ S 1 , S 2 , . . . , S n ]. Equivalently, an activity is an ordered list of pairs of context items.

It is worth noting that our model does not concern itself with the internal processing of patterns as functions, nor with the mapping between states and actions in the program space and those in the LLM space. Instead, it focuses exclusively on the structural properties of their inputs and outputs.

This design choice is motivated by two considerations. First, without such an abstraction, it would be impossible to achieve our original goal of providing an implementation-independent model. Second, this abstraction is practically justified, as we have implemented an interoperability layer that enables automated bidirectional mappings between states and actions in the program space and their counterparts in the LLM space, thereby eliminating the need to explicitly model these details.

## 2.2.2 Special Functions

With the symbols defined in Section 2.2.1, contexts and patterns can be described in a straightforward manner. However, additional functions are required to capture the dynamic processes of agent activities. These functions are denoted by bold uppercase Latin letters.

Session Function, S ( I i ) →O i This function takes a context (a context item, to be more specifically) as the input, passes this context to the agent, and returns the response context from the agent. According to the definition, it is a transform pattern.

Result Function, R a,b ( α, β, . . . ) → a, b This function parses the response context into program variables. In essence, R acts as the inverse of the context-pattern function: while context patterns map variables (if any) to context items, the result function conversely maps context items back to variables. As a usage example, a = R ( α ) or R a ( α ) means parse the variable a from the context item α ; this variable i can be used in patterns after this process.

Many existing studies devote substantial discussion to the interaction between programs and LLMs, as well as to the serialization and deserialization of program states. In contrast, our model introduces the session function S and R to abstract away these engineering details, thereby preventing subsequent analysis from being distracted by such implementation-specific concerns.

As with these two functions, our model delineates a clear boundary between what should be discussed and what should be omitted. This design represents a direct effort to prevent engineering complexity from further permeating academic research.

## 2.3 Unifying Advanced Concepts

In this subsection, we demonstrate the formal expressions of common components and advanced concepts such as agent memory, retrieval-augmented generation (RAG), and in-context learning within our model. This serves to substantiate that the proposed model achieves its intended goal, namely, significantly reducing the number of overlapping concepts in existing agent research.

## 2.3.1 Agent Memory &amp; Chat History

Agent memories, also known as chat histories, refer to all context items across different sessions in an agent activity. For an agent activity A = [ S 1 , S 2 , . . . , S n ], an agent memory is a pattern M defined in Equation 1.

<!-- formula-not-decoded -->

The context pattern of an ordinary chatbot with chat history, F chatbot ( α i ), which has a parameter α i as text written by the user in the i -th turn, can be defined as Equation 2

<!-- formula-not-decoded -->

Moreover, we can present an expression of the response text β i generated by the LLM as Equation 3.

<!-- formula-not-decoded -->

## 2.3.2 Agent Action &amp; Chat Tool

Agent actions are interfaces to program functions for LLMs to use. Generally, an agent action consists of a program function, textual information about this function and its parameters, and a set of serializers for converting parameters and the return value between textual representations in context space and values in program space. An agent action, F action , which proxies a program function A action ( p ) → r , can be defined as Equation 4. In this equation, R -1 is the inverse function of the result function R . R parses program variables from context items, and R -1 conversely serializes program variables into context items.

<!-- formula-not-decoded -->

In practice, when an LLM invokes external tools, the results produced by these tools are typically appended to the context. This side effect of tool invocation under an agent action F action can be described by Equation 5.

<!-- formula-not-decoded -->

## 2.3.3 In-context Learning

The essence of in-context learning (ICL) is injecting and replacing examples within the context.

A piece of an example is denoted as a tuple consisting of a user message in the form of a question β Q and an agent message that provides a verifiable answer β A ; the i -th example in an example buffer is denoted as ( β (Q ,i ) , β (A ,i ) ). The example buffer can be denoted as a set of examples: E = { ( β (Q ,i ) , β (A ,i ) ) | i = 1 , . . . , n } . Given the question α , an in-context learning pattern F ICL can be defined as Equation 6

<!-- formula-not-decoded -->

Consider a function F correct ( α Q ) that, for any given query parameter α Q , produces the correct answer to the associated question. The algorithm for updating the example buffer E can be defined as Algorithm 1.

## Algorithm 1 Updating the Example Buffer

if α (A ,i ) = F correct ( α (Q ,i )) then Randomly pick an example e j from the example buffer E . e j ← ( α (Q ,i ) , α (A ,i ) ) else E ← E ∪ { ( α (Q ,i ) , F correct ( α (Q ,i ))) } end if

Alternatively to the updating algorithm Algorithm 1, the example buffer can also be considered a set containing all historical tuples of questions and answers, and a custom importance function is deployed to rank these examples by their 'educational

value'; for example, examples for which the LLM has previously output the incorrect answer have higher chances of being selected.

From the Equation 6, we can see that it has the same structure as the Equation 1 of the agent memory, except that the collections have different semantic meanings and updating algorithms. This may encourage the belief that agent memories and ICL probably rely on the same fundamental mechanism, and that ICL works because LLMs possess an internal drive to reproduce their own prior behaviors.

This example also demonstrates the power of the Structural Context Model; it provides a rigorous method to identify the similarities and differences in current engineering practices from a structural perspective.

## 2.3.4 Retrieval Augmented Generation

A retrieval augmented generation (RAG) pattern takes text as input and returns related supplementary materials as output. Typically, it uses cosine similarity of embedding vectors to search for the most related supplementary materials.

A RAG pattern can be provided in two different manners: 1) provided as an agent tool (as explained in Section 2.3.2); 2) provided as a transform pattern that receives query text as the parameter and returns the retrieved supplementary materials. Here, we present the definition for the latter one as F RAG in Equation 7. In this equation, the query text is denoted as α ; the i -th supplementary material is defined as β i ; the knowledge database is denoted as B = { β 1 , . . . , β n } , which is a set of supplementary materials.

<!-- formula-not-decoded -->

Comparing Equation 6 of ICL and Equation 7 of RAG, it is obvious that both ICL and RAG are injecting data into the context; the fundamental difference lies in the data source and the way this data is selected: ICL selects examples from the example buffer based on their importance, while RAG selects supplementary materials from the knowledge database based on their cosine similarities with the query text.

## 2.3.5 Multi-agent Collaboration

In current engineering practice, the main purpose of multi-agent collaboration is to decompose a complex task into several relatively easier ones and delegate them to sub-agents specifically customized for heterogeneous tasks.

Since the meaning of sub-agents is to generate results of decomposed sub-tasks for the main agent to use, they can be modeled as context patterns.

Equation 8 is an example of a compositional agent F main that answers questions about physics and chemistry. It has four sub-agents, F physics , F chemistry , F RAG , and F router . Sub-agents F physics and F chemistry are agents customized for their corresponding domains, with specially designed prompts; sub-agent F RAG is the RAG pattern defined in Section 2.3.4; sub-agent F router ( α ) →{ 0 , 1 } is the pattern that determines

whether the query text α is more related to physics (when it returns 0) or chemistry (when it returns 1).

<!-- formula-not-decoded -->

## 2.4 Comparing Agents at a Fundamental Level

By abstracting away the execution and implementation-level engineering details of agents, the Structural Context Model enables analysis and comparison of agent designs and behaviors from a more fundamental perspective.

In this subsection, we demonstrate this key capability by comparing ReAct -based and MLDT -based agents through their formal expressions under the Structural Context Model .

Denote the problem description and rules as α , action lists as β . For the i -th step, denote the current status as γ i . Denote the agent memory pattern as M ( | A ) from Section 2.3.1.

The context patterns corresponding to reasoning and to the ReAct -based agent can be formally defined as shown in Equation 9. Similarly, the context patterns of the task-decomposed MLDT agent can be given as Equation 10. The primary differences between the two agents are highlighted in red.

<!-- formula-not-decoded -->

<!-- formula-not-decoded -->

From the formal expressions of the two agents, their primary differences can be identified both intuitively and straightforwardly. Specifically, the key distinction lies

in the prompt-level roles of the information injected into the agent's context prior to action execution, namely, reasoning versus planning .

In addition, the ReAct agent performs a reasoning step before each action is taken. In contrast, the original formulation of MLDT does not specify conditions or mechanisms for plan adjustment or updating; as a result, it can be interpreted as generating a static plan based on the initial state γ 0 .

Given that ReAct was proposed in 2022 and MLDT in 2024, and considering both the substantial temporal gap and their influence, we along with other researchers, initially expected the two approaches to exhibit significant structural differences. However, the analysis and comparison conducted under the Structural Context Model yield a markedly different conclusion. This further demonstrates the strong capability of the model to reveal the essential nature of agent design.

## 2.5 Engineering Efforts

Building upon the proposed model, we have implemented an open-source framework, ContextCompose , which enables rapid construction and iterative refinement of agent designs through the composition of reusable context patterns. Inspired by declarative UI frameworks, ContextCompose introduces the notion of composing into the domain of agent design, whereby new context patterns are designed and described through the composition of existing ones.

This paradigm substantially increases the modularity and reusability of agent software artifacts, thereby significantly accelerating both development and research iteration cycles.

This paper focuses primarily on the theoretical contributions of the proposed model; the innovations of the framework from the perspective of agent software engineering will be elaborated in separate publications.

## 3 Semantic Dynamics Analysis for Pattern Discovery

In this section, we present Semantic Dynamics Analysis , a method for analyzing the functional roles and relative contributions of different context parts. By characterizing how context parts influence agent behavior, this method facilitates the identification and abstraction of reusable context patterns from existing agent designs.

Beyond its analytical utility, Semantic Dynamics Analysis also highlights a key distinction between our Structural Context Model and existing approaches. Rather than focusing solely on whether specific improvements derived from a method are effective, we additionally emphasize the sustainability of the method itself, that is, whether it enables readers to continuously and independently discover new improvement opportunities.

## 3.1 Theoretical Roots of Dynamic Semantics

The core idea of Dynamic Semantics is that it considers the meaning of a sentence as its 'potential' to update the context [15]. Under classic static semantics, such as compositional semantics, knowing the meaning of a sentence amounts to knowing the

conditions under which it is true. In contrast, Dynamic Semantics defines meaning in terms of how a sentence transforms the current information state.

Dynamic Semantics is particularly well suited to our setting, where context is analyzed through its incremental construction and the resulting changes in context-level metrics. Concretely, the analysis begins by resetting the context to an empty state, and then progressively reconstructing it by reintroducing context components in their original order, while observing the induced semantic dynamics. This controlled reconstruction process enables us to identify the functional roles of individual components as well as the magnitude of their influence.

## 3.2 Definition and Indicators

In this section, we explain several technical terms used in this analytical method and the proposed indicators. Figure 4 provides an intuitive illustration of the main terms and indicators used in Semantic Dynamics Analysis .

Fig. 4 : Intuitive description of key terms in Semantic Dynamics Analysis . ∆Semantics measures the semantic distance between successive prefixes, capturing the semantic update potential introduced by the appended token. Global Drift measures the semantic distance between a prefix and the full text, indicating the extent to which the overall meaning would differ if the text were to end at the current prefix. Global ∆Drift measures the rate at which the current prefix approaches the final meaning of the full text, reflecting the velocity of semantic convergence.

<!-- image -->

Token A token is the minimal unit for LLMs (the Transformer architecture [16], to be more specific) to process. Tokens are acquired from text by a tokenizer with a static dictionary that maps text parts to a specific integer identifier (token). The i -th token is denoted as τ i ; naturally, for any text, the 0-th token is empty: τ 0 = ε . Prefix Text Prefix texts are cumulative partial texts built from the token sequence, e.g., the first prefix is token 1, the second is tokens 1 to 2, and so on. For a text γ = τ 1 · τ 2 . . . τ n , the i -th prefix is denoted as π i := τ 1 · τ 2 . . . τ i ; naturally, the 0-th prefix is empty: π 0 = ε , and the n -th prefix is the text itself: π n = γ . Meanwhile,

- according to the definition that texts are also context items in Section 2.2.1, tokens, token spans, and prefix texts are all elements of the context item space Ω.
- Semantics The semantics of a text α is defined as a set S α of context-updating potentials. The notion of 'context' here does not refer to a LLM context but follows Dynamic Semantics that is introduced in Section 3.1. For two texts α and β , we write S α ⊇ S β to indicate that α semantically subsumes β .
- Embedding Vector &amp; Semantic Centroid An embedding vector is a high-dimensional numerical representation generated by an embedding model from an input text. Embedding vectors are typically normalized to lie on a unit hyper-sphere in the embedding space, which allows semantic relatedness between texts to be measured via geometric distances. For a given text (or token span), its embedding vector is constructed such that the distance to embedding vectors of semantically more related texts is smaller than the distance to those of less related texts, under the same representation model.
- Moreover, for a token span with complex semantic content, its semantic centroid can be viewed as a composition of multiple relatively independent semantic sub-centroids , each corresponding to a coherent semantic component whose mutual relatedness is comparatively low.
- In this work, an embedding vector is treated as a representation-valued functional of the semantic set S associated with a token span. The embedding vector reflects the semantic centroid of the token span in the embedding space, providing a measurable geometric proxy for its underlying semantic structure.
- Semantic Distance Semantic distance quantifies the degree of semantic divergence between two texts in an embedding space, reflecting how dissimilar their meanings are under a given representation model. It is defined as a geometric measure between the embedding vectors associated with the texts. Denoting the embedding vector of a text α by v α , the semantic distance between two texts α and β is measured by the cosine similarity of their embedding vectors, as shown in Equation 11:

<!-- formula-not-decoded -->

Here, we adapt embedding vectors as the measurable estimation of semantics, rather than the internal states of LLM encoders. Due to the attention mechanism [16] of LLMs, it is internal states generated by the LLM encoder that directly determine the outputs of the whole LLM. However, due to multiple concerns, directly observing internal states is not the most suitable approach. The first concern is that internal states can only be acquired from self-hosted open-source models. Adapting this indicator will constrain this analytical method to a limited range of scenarios. The second concern is that the values of internal states vary according to different model architectures and even different versions of the same model. It is almost impossible to define indicators and theorems that are stable across models based on these unstable

representations. The third concern is that these internal states are sensitive to finetunings, especially fine-tuning over specific instruction keywords. This property makes it unsuitable for revealing generally applicable properties and rules of context patterns. Therefore, we adapt embedding vectors as indirect estimation of LLM internal states.

∆ Semantics The ∆Semantics of a token is defined as its semantic contribution to a prefix, measured by the semantic distance between the embeddings of relevant prefixes.

- For a contiguous token span τ i → j := τ i · τ i +1 . . . τ j , its ∆Semantics with respect to the original prefix context is defined as ∆ S ( τ i → j ) := semdist( π j , π i -1 ).
- The token-level ∆Semantics is a special case when i = j , i.e., ∆ S ( τ i ) = ∆ S ( τ i → i ) = semdist( π i , π i -1 ).
- By convention, the first token has ∆Semantics equal to 0, since no prior prefix exists for comparison.
- More generally, when the ∆Semantics of a token span τ i → j is measured relative to an external base text γ , the base text must be explicitly specified: ∆ S γ ( τ i → j ) = semdist ( γ · ( τ i · τ i +1 . . . τ j ) , γ )

This measure reflects the relative importance of a token within the prefixes that contain it.

- Global Drift The semantic distance between each prefix and the full-text (i.e., the final prefix), reflecting how far each prefix text is from the complete meaning: D ( π i ) = semdist( π i , π n ). Intuitively, the Global Drift provides a geometric approximation of the distance from the prefix to the semantic centroid of the text in the embedding space.

Global ∆ Drift The Global ∆Drift is defined as the change in Global Drift between successive prefix texts: ∆ D ( π i ) = D ( π i -1 ) -D ( π i )

- By convention, the first token has a value of 0 since there is no earlier prefix.
- This definition is chosen such that a positive value of ∆ D ( π i ) indicates that the prefix moves closer to the final semantic state, while a negative value indicates divergence.
- From an interpretative perspective, the Global ∆Drift quantifies the contribution of the current token to the semantic progression of its associated prefix. A larger Global ∆Drift suggests that the token induces a substantial shift in the prefix semantics. In particular, pronounced peaks in Global ∆Drift often signal the onset of a transition from one semantic sub-centroid to another, thereby marking a boundary between the respective influence ranges of different semantic sub-centroids.

To provide an intuitive understanding of the components of Semantic Dynamics Analysis , Figure 5 illustrates the analysis results for a task-decomposition prompt excerpted from an open-source prompt library.

When semantic analysis is applied to identify natural separation points in an existing context structure, Global ∆Drift captures the rate at which each prefix approaches

Semantic Dynamics

Fig. 5 : Results of Semantic Dynamics Analysis for a task-decomposition prompt excerpted from an open-source prompt library. The blue line represents Global Drift, the blue bars indicate ∆Semantics, and the orange bars denote Global ∆Drift. Red circles mark potential semantic separation points, where Global ∆Drift exhibits relatively large values.

<!-- image -->

the overall semantic centroid of the full text. Variations in this rate indirectly indicate the presence of semantic sub-centroids along the context trajectory. Figure 6 provides an intuitive illustration of how semantic sub-centroids influence Global ∆Drift. The first and second semantic sub-centroids are indicated by light-blue and blue waypoints, respectively, while the semantic centroid of the full text is marked by a dark-blue waypoint. As tokens are appended, the current prefix evolves along the actual semantic transition trace, shown as a solid orange curve. Owing to the presence of semantic subcentroids, the rate at which the prefix approaches the overall semantic centroid, that is, Global ∆Drift, varies over time, rather than following the optimal transition trace depicted by the blue dashed curve, which consistently attains the maximum Global ∆Drift. A sudden and significant change in Global ∆Drift therefore indicates a sharp shift in the direction of semantic convergence, suggesting that the current prefix lies at a potential semantic sub-centroid.

In summary, Global ∆Drift is used to identify semantic segmentation points within the context structure. ∆Semantics characterizes the relative importance of individual tokens within a segment. The overall trend of Global Drift over a segment reflects the contribution of that segment to the entire context structure.

## 3.3 Geometric Structure of Context Pattern Semantics

With the indicators introduced in Section 3.2, we are able to analyze the semantic properties and functional roles of context patterns.

Fig. 6 : A schematic illustration showing how semantic sub-centroids influence the Global ∆Drift. The first semantic sub-centroid is marked by a light-blue waypoint, the second semantic sub-centroid by a blue waypoint, and the semantic centroid of the full text by a dark-blue waypoint. As each token is appended, the current prefix moves along the actual transition trace, shown as a solid orange curve. Due to the presence of semantic sub-centroids, the rate at which the prefix approaches the overall semantic centroid (which is the Global ∆Drift) varies over time, rather than following the optimal transition trace indicated by the blue dashed curve, which always possess the maximum Global ∆Drift. The sudden significant change of Global ∆Drift indicate the direction of current prefix approaching the overall semantic centroid has dramatically changed, and thus suggests that the current prefix is semantically close to a semantic sub-centroid.

<!-- image -->

## 3.3.1 Semantic Properties and Relationships

When studying the properties of (instantiated) context patterns in a context, we mainly focus on the following special properties and relationships:

Semantic Inclusion For two token spans α and β , we say α semantically includes β if: S α · β = S β · α = S α . From an engineering perspective, if a pattern α semantically

- includes β , then appending β to a context that already contains α is expected to have no additional effect on the resulting context semantics.
- Semantic Parallelism For two token spans α and β , we say α and β are semantically parallel if: S α = S β , i.e., both S α ⊂ S β and S α ⊃ S β hold. Semantic parallelism is transitive: if α is semantically parallel to β and β is semantically parallel to γ , then α is semantically parallel to γ . From an engineering perspective, semantically parallel patterns are interchangeable: using any one or any combination of them is expected to produce the same effect on the context.
- Semantic Orthogonality For two texts α and β , we say α and β are semantically orthogonal if: S α ∩ S β = ∅ . In this case, neither α nor β semantically includes the other. Moreover, the concatenation of semantically orthogonal texts is orderinvariant. From an engineering perspective, removing any pattern from a set of orthogonal patterns in a context is expected to cause a substantial change in the resulting semantics.
- Semantic Order-Invariance For two token spans α and β , we say the combination set { α, β } is semantically order-invariant if: S α · β = S β · α . From an engineering perspective, the relative order of order-invariant combinations does not affect the context semantics.
- Semantic Idempotence A token span α is semantically idempotent if for any positive integer k &gt; 1, it can be repeated for k times in the context and have the same effect on the context semantics: S α = S α k where α k := α · α · · · α ︸ ︷︷ ︸ .
- It can be proven that a token span is semantically idempotent if and only if it is semantically absorbable by itself.

k

times

- From an engineering perspective, semantically idempotent patterns instantiated with identical parameters need not be included multiple times, as repeated occurrences can be safely reduced to a single instance to save tokens. Conversely, when different patterns inevitably contain the same semantically idempotent sub-patterns, no special handling is required for such repetitions, since they do not introduce additional semantic effects.

## 3.3.2 Bridging Semantic Properties and Measurable Indicators

Based on the indicators defined in Section 3.2 and the properties established above, we state the following propositions: Proposition 1, Proposition 2, Proposition 3 , and Proposition 4.

Proposition 1 (Sub-additivity of ∆S under Semantic Inclusion) Let α and β be two token spans. If α semantically includes β , then for any base text γ ∈ Ω ,

<!-- formula-not-decoded -->

Proposition 2 (Super-additivity of ∆S under Semantic Orthogonality) Let α and β be two token spans. If α and β are semantically orthogonal, then for any base text γ ∈ Ω

<!-- formula-not-decoded -->

Proposition 3 (Approximate ∆S Idempotence under Semantic Idempotence) Let α be a token span. If α is semantically idempotent, then for a user-defined tolerance threshold η &gt; 0 and any base text γ ∈ Ω ,

<!-- formula-not-decoded -->

Proposition 4 (Approximate ∆S Invariance under Semantic Order-Invariance) Let α and β be two token spans. If the combination set of { α, β } is semantically order-invariant, then for a user-defined tolerance threshold η &gt; 0 and any base text γ ∈ Ω ,

<!-- formula-not-decoded -->

These propositions bridge the special semantic properties and relationships with measurable indicators derived from embedding vectors, thereby enabling their discovery through mathematical and algorithmic methods.

Moreover, these propositions also contributes to a shift in context engineering from predominantly experience-driven, heuristic empirical studies toward computable and analyzable data-driven research.

## 3.4 Methodology

In this section, we describe a workflow for discovering reusable patterns from existing prompts, which consists of two main steps: segmentation and parameterization.

We have developed a software tool to analyze a prompt and generate the corresponding analysis charts; however, the level of automation remains limited. In particular, the parameterization step still requires manual intervention.

The workflow consists of the following steps:

1. Tokenization. A tokenizer is applied to the prompt to obtain a token sequence, which serves as the input for subsequent analysis.
2. Semantic dynamics analysis. Based on the definitions in Section 3.2, we compute the ∆Semantics for each token, as well as the Global Drift and Global ∆Drift for each prefix.
3. Segmentation via Global ∆ Drift. Prefixes associated with abnormally high Global ∆Drift values are identified. As discussed in Section 3.2, such anomalies suggest the presence of nearby semantic sub-centroids. The ending tokens of these prefixes are therefore used as semantic separation points, partitioning the prompt into multiple segments.
4. Property verification of segments. The propositions introduced in Section 3.3.2 are applied to examine whether the extracted segments exhibit the semantic properties and relationships described in Section 3.3.1.
5. Segment refinement. Within each segment, tokens with high ∆Semantics are treated as locally important, whereas tokens with relatively low ∆Semantics are considered less influential and may be removed with limited impact on the local semantics.
6. Parameterization of influential content. Token spans exhibiting both high ∆Semantics and high Global ∆Drift are regarded as semantically influential at

both the segment and prompt levels. Such spans are natural candidates for parameterization, as modifying or replacing them typically induces substantial semantic changes. The final decision of parameterization remains flexible and can be guided by practical engineering requirements.

## 3.4.1 Demonstration on a Task Decomposition Agent

Text 1 shows an example prompt for task decomposition excerpted from an opensource prompt library Fabric 1 .

In this section, we demonstrate how Semantic Dynamics Analysis can be applied to existing context structures to extract reusable patterns, which are later employed in the our agent designs presented in Section 4.

## Text 1: Prompt for Task Decomposition

You are an AI assistant specialized in task decomposition and recursive outlining. Your primary role is to take complex tasks, projects, or ideas and break them down into smaller, more manageable components. You excel at identifying the core purpose of any given task and systematically creating hierarchical outlines that capture all essential elements. ...... STEPS - Identify the main task or project presented by the user - Determine the overall purpose or goal of the task - Create a high-level outline of the main components or sections needed to complete the task ...... OUTPUT INSTRUCTIONS - Only output Markdown. - Use hierarchical bullet points to represent the recursive nature of the outline. - Main components should be represented by top-level bullets ......

The detailed results of Semantic Dynamics Analysis are provided in Appendix A. Figure 7 illustrates the semantic segments identified based on these results. According to the analysis, the prompt can be partitioned into four segments: (1) role settings, (2) goal settings, (3) detailed steps, and (4) output format settings. Moreover, the ∆Semantics values reported in Table A1 indicate that segment pairs (1, 2) and (3, 4) exhibit relatively strong semantic order invariance.

Among them, the segment corresponding to detailed steps is reused in the agent designs presented in Section 4.

## 4 Case Study

In this chapter, we conduct a comprehensive case study to demonstrate the applicability of the methods introduced in Section 2 and Section 3. Based on these methods, we design three novel agents with different context structures. In addition, we reproduce several representative baselines for comparison, including the widely adopted ReAct agent, the MLDT agent, and a minimal baseline agent that follows a simple observe-act loop without explicit planning or memory mechanisms.

1 The URL to the repository: https://raw.githubusercontent.com/danielmiessler/Fabric.

Fig. 7 : Semantic segments identified from the results of Semantic Dynamics Analysis . The prompt is partitioned into four segments: role settings, goal settings, detailed steps, and output format settings. The segment corresponding to detailed steps is reused in the agent designs presented in Section 4.

<!-- image -->

All 6 agents are evaluated in a suite of 15 task scenarios with varying dynamic factors and difficulty levels. Each agent is tested with 100 independent repeated trials per scenario to ensure statistical robustness.

## 4.1 Problem Settings

The monkey-banana problem [17] is a classic benchmark for evaluating robotic task planning. As illustrated in Figure 8, the objective of the task is to control the monkey, manipulate boxes, and ultimately obtain the banana.

Building upon the classic monkey-banana problem, we introduce a set of dynamic factors to construct a series of variant monkey-banana problems, making the task setting closer to the real-world challenges faced by modern robotic intelligent systems. These dynamic factors violate the core assumptions relied upon by classical planners, including a static environment, deterministic action outcomes, and complete prior knowledge. As a result, such variants cannot be solved within the classical planning paradigm. This further underscores the importance of LLM-based task agents.

The problem scenarios in our experiments are organized into five major categories: Classic , Dual Bananas , Shortsighted Monkey , Over-weight Monkey , and Comprehensive . Each category is further instantiated at three difficulty levels: easy , medium , and hard . In the remainder of this paper, we refer to these problem instances uniformly as scenes , following the terminology used in our simulator. The elaboration of different difficulty levels are detailed in Section 4.1.1, while the elaboration of different categories are provided in Section 4.1.2.

## 4.1.1 Difficulty Factors

Scenes with different difficulty levels are illustrated in Figure 9.

Fig. 8 : The classic monkey-banana problem. The goal of this problem is to control the monkey, manipulate boxes, and ultimately obtain the banana.

<!-- image -->

Fig. 9 : Overview of monkey-banana problem scenes with increasing difficulty. The difficulty levels are determined by both the number of target bananas (single or dual) and the complexity of the environment configuration, resulting in progressively more demanding task planning scenarios.

<!-- image -->

In the easy scenes, the environment contains a single small box and a single elevated platform. This setting is designed to verify whether an agent possesses the basic capability to solve the monkey-banana problem.

In the medium scenes, the environment includes one small box and one large box. The monkey must sequentially climb the small box and then the large box in order to reach the platform holding the banana. This setting primarily evaluates the agent's reasoning ability under multi-step action dependencies.

In the hard scenes, the environment contains two platforms arranged at different heights. To reach the second platform where the banana is placed, the monkey must carry a small box, sequentially climb the small box and the large box, and place the small box onto the first platform. Beyond further increasing reasoning complexity, this setting also enables evaluation of the agent's foresight, as reflected by the number of steps required to anticipate the need to transport an additional box in advance.

## 4.1.2 Scene Categories

In total, the experimental setup consists of 15 scenes, indexed by Scene IDs from 1 to 15. These scenes are organized into five distinct categories, with each category comprising three instances corresponding to the different difficulty levels defined in Section 4.1.1. Within each category, the difficulty increases with the scene index. For example, Scenes 1 ∼ 3 correspond to the classic monkey-banana problem, where Scene 1 represents the easy setting, Scene 2 the medium setting, and Scene 3 the hard setting.

- (ID 1 ∼ 3) 'Classic' These scenes correspond to the canonical monkey-banana problem instantiated at different difficulty levels.
- (ID 4 ∼ 6 ) 'Dual Bananas' In this category, two bananas are provided within the scene. The objective is to evaluate the agent's ability to consistently commit to a single target when multiple equivalent goals are available, rather than exhibiting indecisive or oscillatory behavior. Notably, in the hard setting, the increased difficulty is not introduced by adding an additional platform. Instead, the two targets incur different achievement costs, with one banana being easier to obtain than the other, thereby testing the agent's ability to flexibly assess and select among competing goals.
- (ID 7 ∼ 9) 'Shortsighted Monkey' This category violates the classical planner assumption of complete predefined knowledge. In these scenes, when the distance between the monkey and a box exceeds the interaction range, the simulator no longer provides detailed information about that box, such as its dimensions, and instead marks it as unknown . This category is designed to evaluate the agent's memory capability.

Figure 10 provides an intuitive illustration of this characteristic. In the figure, the gray boxes indicate objects whose detailed information is rendered as unknown because they are beyond the monkey's interaction range.

- (ID 10 ∼ 12) 'Over-weight Monkey' This category violates the classical planner assumption of deterministic action outcomes. In these scenes, whenever the monkey climbs onto a box, there is a 50% probability that the box will be crushed, reducing its height to a random proportion of its original height (ranging from 50% to 70%). The probability of being crushed differs across boxes. As a result, a platform that would normally be reachable by stacking one large box and one small box may instead require an additional small box placed on top of the crushed large box if the latter collapses.

Many agents perform multiple attempts within a scene. Such irreversible side effects caused by crushing boxes can invalidate the scene knowledge acquired from previous attempts, thereby further evaluating the agent's ability in integrated

- analysis, reasoning, and online learning. Figure 11 provides an illustration of this mechanism.
- (ID 13 ∼ 15) 'Comprehensive' This category simultaneously enables the dynamic elements from 'Shortsighted Monkey' and 'Over-weight Monkey'. Specifically, detailed box information becomes unavailable once it leaves the interaction range, and boxes may be crushed with a certain probability when the monkey climbs onto them. These scenes are designed to comprehensively evaluate an agent's ability to analyze and respond to uncertainty in highly complex environments. Moreover, since the probabilistic side effects of crushing boxes can frequently alter box heights, related information stored in the agent's memory may become invalid. As a result, this category additionally evaluates the agent's ability to assess the validity of its memory and proactively update it.

Fig. 10 : Illustration of the 'Shortsighted Monkey' mechanism. When a box moves beyond the monkey's interaction range, its detailed information becomes unavailable. The dark-colored boxes in the figure indicate objects whose attributes are marked as unknown due to being outside the interaction range.

<!-- image -->

## 4.2 Comparison Agents

In this experiment, a total of six different agents are evaluated. One agent, referred to as Basic , serves as a baseline and is equipped only with a simple observation-action loop. Two additional agents are reproductions of widely adopted agent architectures, namely ReAct and MLDT . The remaining three agents are newly proposed in

Fig. 11 : Illustration of the 'Over-weight Monkey' mechanism: each time the monkey climbs onto a box, there is a probability that the box will be crushed, reducing its height to a random proportion.

<!-- image -->

this work: 'On-demand Reasoning' ( OR ), 'On-demand Reasoning + Notes' ( ORN ), and 'On-demand Reasoning + Notes + Planning' ( PlanORN ).

The formal representations of ReAct and MLDT under the Structural Context Model have already been presented in Section 2.4. In the following, we provide the corresponding formal representations for the remaining agents.

Denote the problem description and rules as α , action lists as β . For the i -th step, denote the current status as γ i . Denote the agent memory pattern as M ( | A ) from Section 2.3.1.

Equation 12 presents the formal expression of Basic .

<!-- formula-not-decoded -->

From the formal expression of ReAct given in Equation 9, we observe that the ReAct agent performs reasoning at every step within a sub-session under the same context, regardless of whether the agent actually has uncertainty about the next action or about events occurring in the current Scene. This design substantially increases per-step latency and token consumption. Moreover, because consecutive reasoning processes are performed in close proximity in the context, adjacent reasoning outputs are likely to interfere with each other, thereby reducing the overall information gain. A natural direction for improvement is therefore to transform reasoning from a mandatory per-step operation into an optional one.

Based on this idea, we propose the 'On-demand Reasoning' ( OR ) agent. Equation 13 presents its formal expression, with the main differences from ReAct highlighted in red.

Here, the reasoning sub-agent is implemented as an agent tool A reasoning , whose parameter q i represents the main agent's question about the current context or the instruction that the reasoning process is expected to fulfill. From this formal expression, we can also directly observe that, in contrast to the ReAct agent, which injects only the reasoning output into the action context, the proposed OR agent additionally injects the question itself into the main agent's action context.

<!-- formula-not-decoded -->

To enhance memory capability, we propose a variant agent that augments OR with a memory module implemented as a tool, referred to as On-demand Reasoning + Notes ( ORN ). The set of notes is denoted as N = { n 1 , n 2 , ...n m } , Two agent tools, A add-tool ( n ) and A remove-tool ( n ), are provided to add notes to and remove notes from N , respectively. According to prior studies on the attention mechanisms of LLMs [18, 19], these models tend to allocate more attention to tokens near the beginning and the end of the context, while information located in the middle of long contexts is more likely to be overlooked or forgotten. Motivated by this observation, the Notes tool injects the contents of N into the end of the action context before each action step. Compared to having such critical information scattered within a long context, this design makes the notes more likely to receive the model's attention. The formal expression of ORN is given in Equation 14. The differences from the OR agent are likewise highlighted in red.

<!-- formula-not-decoded -->

As analyzed in Section 2.4, MLDT produces a static plan. A natural direction for improvement is therefore to encapsulate the task decomposition and plan generation sub-agent as a tool that can be invoked by the main agent. This tool is denoted as A plan ( g i ), where the parameter g i represents the agent's feedback on the current plan or the objective for generating a new plan. Equation 15 presents the formal expression of PlanORN , with the differences from ORN highlighted in red. The prompt used for plan generation is reused from the Semantic Dynamics Analysis results presented in Section 3.4.1.

<!-- formula-not-decoded -->

## 4.3 Evaluation Results

For the 15 scenes and 6 agents, each agent is evaluated on each scene over 100 independent trials. Each time the agent issues a control command for the monkey (such as horizontal movement, climbing up, climbing down, grabbing a box, or placing a box), it is counted as one step. A trial is considered successful if the agent controls the monkey to reach the position of the banana within 300 steps. Otherwise, the trial is regarded as a failure if the step limit of 300 is exceeded or if the agent explicitly invokes the Abandon agent tool.

In these experiments, we collect and analyze following metrics: (1) success rate; (2) three average indicators computed over all trials (including failures): average steps, average time cost, and average token cost; (3) two specialized metrics, Time-to-Success and Token-to-Success .

Denote the success rate as r , denote the average value of a given metrics X (time consumption, token consumption, etc.) over successful trials as x s , and denote the corresponding average value over failed trials as x f . The definition of X-to-Success is given in Equation 16.

<!-- formula-not-decoded -->

Conventional average metrics (e.g., success rate and average number of steps) reflect the expected performance of an agent under a fixed number of attempts. Different metrics correspond to different real-world considerations: for instance, when the cost of failure dominates, success rate is the primary reference, whereas when the cost of each action is high, the average number of steps becomes more relevant.

In contrast, Time-to-Success and Token-to-Success provide an alternative perspective that is more aligned with practical LLM-powered robot agents, where failures are typically inevitable but acceptable (or recoverable), and the user's instruction only needs to be successfully completed once. These metrics characterize the expected waiting time and LLM token cost incurred by a user.

Table 1 presents the experimental results on the Classic scene category (Scene IDs ranging from 1 to 3). Table 2 presents the experimental results on the Dual Bananas scene category (Scene IDs ranging from 4 to 6). Table 3 presents the experimental results on the Shortsighted Monkey scene category (Scene IDs ranging from 7 to 9). Table 4 presents the experimental results on the Over-weight Monkey scene category

(Scene IDs ranging from 10 to 12). Table 5 presents the experimental results on the Comprehensive scene category (Scene IDs ranging from 13 to 15).

Table 1 : Experimental results on the ' Classic ' scene category. The table reports the success rate (SR. ↑ ), average number of steps (Avg. Steps ↓ ), average execution time (Avg. Time ↓ ), average token consumption (Avg. Tokens ↓ ), as well as the Timeto-Success (Time-to-S ↓ ) and Token-to-Success (Token-to-S ↓ ) metrics for each agent. Agents proposed in this work are marked with an asterisk (*), and the best result for each metric is underlined. Arrows ( ↑ / ↓ ) indicate whether higher or lower values are better, respectively. The symbol ' ∞ ' indicates that no successful trials were observed.

| Scene   | Agent    | SR. ↑     | Avg. Steps ↓   | Avg. Time(s) ↓   | Avg. Tokens ↓   | Time-to-S ↓             | Tokens-to-S ↓   |
|---------|----------|-----------|----------------|------------------|-----------------|-------------------------|-----------------|
| Scene 1 | Basic    | 0.99 0.99 | 7.0 7.5 7.3    | 14.05 40.09      | 11.6K 17.0K     | 14.19 40.49 20.25 14.88 | 11.8K 17.3K     |
| Scene 1 | ReAct    |           |                |                  |                 |                         |                 |
| Scene 1 | MLDT     | 0.98      |                | 19.85            | 15.0K           |                         | 15.6K           |
| Scene 1 | OR*      | 1.00      | 7.1            | 14.88            | 12.7K           |                         | 12.7K           |
| Scene 1 | ORN*     | 0.97      | 7.7            | 18.80            | 21.9K           | 19.38                   | 23.0K           |
| Scene 1 | PlanORN* | 1.00      | 9.0            | 38.97            | 30.7K           | 38.97                   | 30.7K           |
| Scene 2 | Basic    | 0.86      | 23.6           | 45.74            | 212.2K          | 53.18                   | 121.3K          |
| Scene 2 | ReAct    | 0.87      | 30.9           | 232.64           | 596.9K          | 267.40                  | 292.7K          |
| Scene 2 | MLDT     | 0.85      | 28.6           | 65.51            | 359.5K          | 77.07                   | 96.6K           |
| Scene 2 | OR*      | 0.97      | 22.5           | 61.65            | 205.5K          | 63.56                   | 212.9K          |
| Scene 2 | ORN*     | 0.81      | 29.1           | 109.63           | 343.3K          | 135.34                  | 368.2K          |
| Scene 2 | PlanORN* | 0.85      | 34.1           | 166.60           | 750.2K          | 196.00                  | 493.3K          |
| Scene 3 | Basic    | 0.00      | 27.1           | 49.42            | 186.8K          | ∞                       | ∞               |
| Scene 3 | ReAct    | 0.53      | 84.3           | 1066.64          | 4.5M            | 2012.53                 | 910.9K          |
| Scene 3 | MLDT     | 0.00      | 28.1           | 63.27            | 239.0K          | ∞                       | ∞               |
| Scene 3 | OR*      | 0.37      | 70.6           | 291.63           | 2.1M            | 788.18                  | 4.6M            |
| Scene 3 | ORN*     | 0.39      | 72.9           | 423.08           | 3.0M            | 1084.82                 | 7.0M            |
| Scene 3 | PlanORN* | 0.66      | 83.8           | 503.38           | 4.2M            | 762.69                  | 3.2M            |

We estimate the difficulty of each scene by jointly considering the success rates and average numbers of steps achieved by all agents across scenes. Based on this estimation, the scenes are ranked in ascending order of difficulty, as shown in Figure 12. The horizontal axis corresponds to the scene IDs, with the average number of steps indicated in parentheses. The performance of these agents on the five most difficult scenes (ID 14, ID 3, ID 9, ID 12, ID 15) is summarized in Table B2.

From this table, we observe that the 15 scenes exhibit clear separability in terms of difficulty. They can be roughly grouped into four difficulty intervals, corresponding to success-rate ranges of 98.83% ∼ 95.33%, 88.33% ∼ 80.83%, 53.83% ∼ 53.67%, and 32.50% ∼ 16.81%, respectively. Within each interval, the difficulty increases relatively smoothly, enabling a comprehensive evaluation of performance differences among agents. In contrast, the final interval shows a much steeper increase in difficulty, thereby more strongly distinguishing agents that exhibit greater robustness under abrupt increases in task difficulty.

Figure 13 compares the success rates of all agents on the five most difficult scenes, ordered by increasing difficulty. As the scene difficulty increases, a consistent performance degradation can be observed across all agents.

As shown in the figure, both the ReAct agent and the proposed PlanORN agent exhibit relatively strong resilience as scene difficulty increases. Notably, the success

Table 2 : Experimental results on the ' Dual Bananas ' scene category. The table reports the success rate (SR. ↑ ), average number of steps (Avg. Steps ↓ ), average execution time (Avg. Time ↓ ), average token consumption (Avg. Tokens ↓ ), as well as the Timeto-Success (Time-to-S ↓ ) and Token-to-Success (Token-to-S ↓ ) metrics for each agent. Agents proposed in this work are marked with an asterisk (*), and the best result for each metric is underlined. Arrows ( ↑ / ↓ ) indicate whether higher or lower values are better, respectively. The symbol ' ∞ ' indicates that no successful trials were observed.

| Scene   | Agent    |   SR. ↑ |   Avg. Steps ↓ |   Avg. Time(s) ↓ | Avg. Tokens ↓   | Time-to-S ↓       | Tokens-to-S ↓   |
|---------|----------|---------|----------------|------------------|-----------------|-------------------|-----------------|
| Scene 4 | Basic    |    0.97 |            7.4 |            13.54 | 14.7K 20.7K     | 13.95 42.73 20.97 | 15.2K 19.5K     |
| Scene 4 | ReAct    |    0.99 |            7.7 |            42.31 |                 |                   |                 |
| Scene 4 | MLDT     |    0.99 |            7.8 |            20.76 | 19.1K           |                   | 19.3K           |
| Scene 4 | OR*      |    0.99 |            7.3 |            15.16 | 15.7K           | 15.31             | 15.9K           |
| Scene 4 | ORN*     |    0.99 |            8.1 |            18.76 | 25.8K           | 18.95             | 26.2K           |
| Scene 4 | PlanORN* |    1    |            9.1 |            33.05 | 34.8K           | 33.05             | 34.8K           |
| Scene 5 | Basic    |    0.5  |           19.6 |            34.89 | 111.1K          | 69.77             | 200.9K          |
| Scene 5 | ReAct    |    0.88 |           28.6 |           247.76 | 605.7K          | 281.55            | 246.5K          |
| Scene 5 | MLDT     |    0.65 |           17.3 |            39.31 | 93.9K           | 60.48             | 128.3K          |
| Scene 5 | OR*      |    0.94 |           22.4 |            62.41 | 179.5K          | 66.39             | 185.5K          |
| Scene 5 | ORN*     |    0.93 |           23   |            84.25 | 253.1K          | 90.59             | 254.9K          |
| Scene 5 | PlanORN* |    0.95 |           42.5 |           203.04 | 1.1M            | 213.72            | 1.1M            |
| Scene 6 | Basic    |    0.9  |           11.2 |            20.1  | 43.6K           | 22.34             | 38.5K           |
| Scene 6 | ReAct    |    1    |           12.4 |            79.82 | 70.9K           | 79.82             | 70.9K           |
| Scene 6 | MLDT     |    0.9  |           10.6 |            28.91 | 41.7K           | 32.12             | 41.6K           |
| Scene 6 | OR*      |    0.99 |           11.3 |            26.7  | 51.7K           | 26.97             | 48.1K           |
| Scene 6 | ORN*     |    1    |           11.6 |            32.81 | 61.5K           | 32.81             | 61.5K           |
| Scene 6 | PlanORN* |    0.99 |           19.6 |            78.67 | 188.1K          | 79.47             | 182.0K          |

Table 3 : Experimental results on the ' Shortsighted Monkey ' scene category. The table reports the success rate (SR. ↑ ), average number of steps (Avg. Steps ↓ ), average execution time (Avg. Time ↓ ), average token consumption (Avg. Tokens ↓ ), as well as the Time-to-Success (Time-to-S ↓ ) and Token-to-Success (Token-to-S ↓ ) metrics for each agent. Agents proposed in this work are marked with an asterisk (*), and the best result for each metric is underlined. Arrows ( ↑ / ↓ ) indicate whether higher or lower values are better, respectively. The symbol ' ∞ ' indicates that no successful trials were observed.

| Scene   | Agent    | SR. ↑     | Avg. Steps ↓   |   Avg. Time(s) ↓ | Avg. Tokens ↓   | Time-to-S ↓             | Tokens-to-S ↓   |
|---------|----------|-----------|----------------|------------------|-----------------|-------------------------|-----------------|
| Scene 7 | Basic    | 1.00 0.99 | 7.6 8.7        |            15.05 | 14.1K 24.2K     | 15.05 54.00 24.30 16.15 | 14.1K 24.4K     |
| Scene 7 | ReAct    |           |                |            53.46 |                 |                         |                 |
| Scene 7 | MLDT     | 0.96      | 8.4            |            23.33 | 20.3K           |                         | 21.6K           |
| Scene 7 | OR*      | 1.00      | 7.6            |            16.15 | 15.3K           |                         | 15.3K           |
| Scene 7 | ORN*     | 0.99      | 8.3            |            20.71 | 25.3K           | 20.92                   | 25.4K           |
| Scene 7 | PlanORN* | 0.95      | 9.9            |            39.45 | 48.7K           | 41.52                   | 42.6K           |
| Scene 8 | Basic    | 0.66      | 38.0           |            78.81 | 466.9K          | 119.41                  | 338.8K          |
| Scene 8 | ReAct    | 0.86      | 32.4           |           311.66 | 814.8K          | 361.24                  | 215.7K          |
| Scene 8 | MLDT     | 0.60      | 36.8           |            82.44 | 532.8K          | 137.40                  | 182.5K          |
| Scene 8 | OR*      | 0.94      | 29.6           |            83.31 | 264.3K          | 88.62                   | 278.4K          |
| Scene 8 | ORN*     | 0.88      | 26.2           |            96.63 | 279.4K          | 109.80                  | 293.3K          |
| Scene 8 | PlanORN* | 1.00      | 22.4           |            94.34 | 384.0K          | 94.34                   | 384.0K          |
| Scene 9 | Basic    | 0.00      | 31.6           |            63.09 | 456.4K          | ∞                       | ∞               |
| Scene 9 | ReAct    | 0.50      | 105.9          |          1641    | 7.0M            | 3251.04                 | 1.9M            |
| Scene 9 | MLDT     | 0.00      | 21.9           |            53.16 | 172.7K          | ∞                       | ∞               |
| Scene 9 | OR*      | 0.40      | 87.7           |           390.94 | 3.4M            | 977.36                  | 4.5M            |
| Scene 9 | ORN*     | 0.32      | 133.7          |          1077.15 | 10.4M           | 3366.08                 | 9.8M            |
| Scene 9 | PlanORN* | 0.56      | 109.7          |           829    | 9.0M            | 1480.35                 | 6.3M            |

Fig. 12 : Ranking of scene difficulty based on success rates across all agents. Scenes are ordered in ascending difficulty from top to bottom. When multiple scenes share the same average success rate, they are further ordered by the average number of steps in ascending order. The horizontal axis indicates the success rate, while the average number of steps for each scene is shown in parentheses next to the corresponding scene ID.

<!-- image -->

Fig. 13 : Success rates of different agents on the five most difficult scenes, ordered by increasing difficulty.

<!-- image -->

Table 4 : Experimental results on the ' Over-weight Monkey ' scene category. The table reports the success rate (SR. ↑ ), average number of steps (Avg. Steps ↓ ), average execution time (Avg. Time ↓ ), average token consumption (Avg. Tokens ↓ ), as well as the Time-to-Success (Time-to-S ↓ ) and Token-to-Success (Token-to-S ↓ ) metrics for each agent. Agents proposed in this work are marked with an asterisk (*), and the best result for each metric is underlined. Arrows ( ↑ / ↓ ) indicate whether higher or lower values are better, respectively. The symbol ' ∞ ' indicates that no successful trials were observed.

| Scene    | Agent    |   SR. ↑ |   Avg. Steps ↓ |   Avg. Time(s) ↓ | Avg. Tokens ↓   | Time-to-S ↓   | Tokens-to-S ↓   |
|----------|----------|---------|----------------|------------------|-----------------|---------------|-----------------|
| Scene 10 | Basic    |    0.94 |           11.9 |            22.12 | 37.3K           | 23.53 80.51   | 38.8K           |
| Scene 10 | ReAct    |    1    |           10.8 |            80.51 | 45.3K           |               | 45.3K           |
| Scene 10 | MLDT     |    0.94 |            9.7 |            25.78 | 30.8K           | 27.43         | 33.4K           |
| Scene 10 | OR*      |    0.98 |           11.4 |            31.03 | 41.8K           | 31.66         | 43.5K           |
| Scene 10 | ORN*     |    0.9  |           17.3 |            53.95 | 119.9K          | 59.95         | 120.2K          |
| Scene 10 | PlanORN* |    0.96 |           15.1 |            62.13 | 121.7K          | 64.72         | 122.3K          |
| Scene 11 | Basic    |    0.3  |           22.2 |            41.97 | 117.7K          | 139.90        | 322.9K          |
| Scene 11 | ReAct    |    0.57 |           67.5 |           781.7  | 2.3M            | 1371.41       | 1.3M            |
| Scene 11 | MLDT     |    0.4  |           20.5 |            45.38 | 111.7K          | 113.46        | 277.6K          |
| Scene 11 | OR*      |    0.82 |           35.4 |           109.43 | 437.9K          | 133.45        | 376.1K          |
| Scene 11 | ORN*     |    0.47 |           40.7 |           172.64 | 739.4K          | 367.32        | 1.4M            |
| Scene 11 | PlanORN* |    0.67 |           52.8 |           324.53 | 2.2M            | 484.38        | 2.7M            |
| Scene 12 | Basic    |    0    |           25.7 |            46.49 | 189.8K          | ∞             | ∞               |
| Scene 12 | ReAct    |    0.33 |          106.9 |          1541.9  | 5.5M            | 4671.04       | 7.0M            |
| Scene 12 | MLDT     |    0    |           26.6 |            62.15 | 213.3K          | ∞             | ∞               |
| Scene 12 | OR*      |    0.26 |           78.5 |           385.49 | 3.0M            | 1482.66       | 8.3M            |
| Scene 12 | ORN*     |    0.22 |           66.3 |           390.96 | 2.9M            | 1777.07       | 21.4M           |
| Scene 12 | PlanORN* |    0.5  |           99.6 |           726.11 | 6.8M            | 1452.21       | 10.2M           |

rate of PlanORN consistently exceeds that of ReAct across all five scenes. In addition, although the OR and ORN agents experience a pronounced drop in success rate from Scene 14 to Scene 3, their performance subsequently stabilizes at a relatively consistent suboptimal level, indicating a certain degree of robustness under further increases in difficulty.

## 4.4 Result Analysis

In this subsection, we summarize the performance of each agent on the proposed benchmark and analyze the underlying reasons for their observed behaviors from the perspective of context structure.

Overall, PlanORN achieves the strongest performance across the entire benchmark, exhibiting clear advantages in success rates on medium- and high-difficulty scenes and demonstrating strong resilience as task difficulty increases. In contrast, while its capability is limited to simpler settings, Basic offers overwhelming cost efficiency on easy scenes, making it a highly competitive baseline when task complexity is low.

## 4.4.1 ' Basic ' Agent

Basic is the only agent among the six that does not incorporate any sub-agent component, i.e., its formal expression does not contain S ( ... ). As a purely reactive baseline, it

Table 5 : Experimental results on the ' Comprehensive ' scene category. The table reports the success rate (SR. ↑ ), average number of steps (Avg. Steps ↓ ), average execution time (Avg. Time ↓ ), average token consumption (Avg. Tokens ↓ ), as well as the Time-to-Success (Time-to-S ↓ ) and Token-to-Success (Token-to-S ↓ ) metrics for each agent. Agents proposed in this work are marked with an asterisk (*), and the best result for each metric is underlined. Arrows ( ↑ / ↓ ) indicate whether higher or lower values are better, respectively. The symbol ' ∞ ' indicates that no successful trials were observed.

| Scene    | Agent    | SR. ↑          |   Avg. Steps ↓ | Avg. Time(s) ↓   | Avg. Tokens ↓      | Time-to-S ↓              | Tokens-to-S ↓     |
|----------|----------|----------------|----------------|------------------|--------------------|--------------------------|-------------------|
| Scene 13 | Basic    | 0.97 0.99 0.98 |            8.5 | 16.48 109.50     | 19.3K 173.0K 20.8K | 16.99 110.60 23.00 20.03 | 19.2K 57.0K 21.6K |
| Scene 13 | ReAct    |                |           12.9 |                  |                    |                          |                   |
| Scene 13 | MLDT     |                |            8.3 | 22.54            |                    |                          |                   |
| Scene 13 | OR*      | 1.00           |            8.7 | 20.03            | 21.9K              |                          | 21.9K             |
| Scene 13 | ORN*     | 0.93           |           13.5 | 40.30            | 70.5K              | 43.34                    | 72.2K             |
| Scene 13 | PlanORN* | 0.94           |           13.2 | 51.40            | 91.7K              | 54.68                    | 92.7K             |
| Scene 14 | Basic    | 0.03           |           21.6 | 36.91            | 132.4K             | 1230.25                  | 7.0M              |
| Scene 14 | ReAct    | 0.68           |           71.8 | 856.34           | 2.9M               | 1259.33                  | 1.5M              |
| Scene 14 | MLDT     | 0.12           |           18.4 | 43.40            | 110.2K             | 361.66                   | 1.2M              |
| Scene 14 | OR*      | 0.78           |           40.7 | 133.28           | 579.1K             | 170.87                   | 628.3K            |
| Scene 14 | ORN*     | 0.72           |           54.5 | 276.57           | 1.7M               | 384.13                   | 1.7M              |
| Scene 14 | PlanORN* | 0.89           |           48   | 253.78           | 1.6M               | 285.15                   | 1.3M              |
| Scene 15 | Basic    | 0.00           |           21.9 | 41.24            | 174.8K             | ∞                        | ∞                 |
| Scene 15 | ReAct    | 0.18           |          120.1 | 1880.30          | 7.4M               | 10446.09                 | 16.3M             |
| Scene 15 | MLDT     | 0.00           |           23.3 | 60.95            | 235.8K             | ∞                        | ∞                 |
| Scene 15 | OR*      | 0.27           |          111.9 | 691.11           | 6.4M               | 2585.26                  | 17.4M             |
| Scene 15 | ORN*     | 0.24           |          129   | 1077.71          | 10.9M              | 4490.45                  | 30.4M             |
| Scene 15 | PlanORN* | 0.32           |          115.6 | 793.93           | 8.5M               | 2481.03                  | 17.9M             |

exhibits extremely low time and token consumption in simple scenes where the environment is fully observable and deterministic. However, once the scene slightly violates these assumptions, its success rate drops sharply, indicating a lack of robustness to uncertainty and delayed effects.

## 4.4.2 ReAct Agent

ReAct augments the Basic agent with an explicit reasoning sub-agent, enabling iterative reflection and action revision. This mechanism improves robustness in moderately difficult scenes but incurs substantial computational overhead, resulting in significantly increased time and token consumption as scene complexity grows.

## 4.4.3 MLDT Agent

MLDT exhibits a largely mediocre overall performance across the evaluated scenes. Although its task decomposition sub-agent produces an explicit static plan, this plan does not translate into a success-rate advantage in difficult scenarios. In particular, when classical planning assumptions are violated, the success rate of MLDT drops to nearly zero, indicating that the static plan is insufficient to handle uncertainty, partial observability, or irreversible side effects.

Nevertheless, the same static planning mechanism enables MLDT to terminate unsuccessful attempts more quickly once the precomputed plan becomes infeasible.

As a result, in several moderately difficult scenes (e.g., Scenes 5, 8, 10, and 11), MLDT achieves lower average execution time and, in some cases, smaller Tokens-to-Success compared to more adaptive agents. This efficiency, however, stems primarily from early abandonment rather than improved problem-solving capability.

Overall, MLDT demonstrates that task decomposition alone, when coupled with a static world assumption, offers limited robustness benefits and cannot support sustained performance in challenging environments.

## 4.4.4 OR &amp; ORN Agents

OR and ORN represent a middle tier of agent designs that introduce structured online reasoning, with ORN further augmenting the architecture with an explicit note-based memory sub-agent. Compared to Basic , ReAct , and MLDT , both agents demonstrate markedly improved robustness across medium and hard scenes, maintaining stable non-zero success rates while incurring substantially lower time and token costs than reasoning-heavy approaches. This indicates that structured reasoning alone already provides a meaningful robustness gain under partial observability and delayed effects.

The additional memory component ('notes') in ORN yields only limited improvements over OR , and primarily in a subset of medium-difficulty scenes. A key limitation lies in the fact that stored notes may become outdated or invalid as the environment evolves, yet are not always updated or cleared in a timely manner. Such stale notes can mislead subsequent reasoning and, in harder scenes, may even degrade performance relative to OR . Moreover, advanced LLMs exhibit a certain degree of robustness to 'context rot', which further dilutes the marginal benefit brought by explicit notes. As a result, ORN improves upon OR only under specific conditions, while failing to provide a consistent advantage in the most challenging scenarios.

## 4.4.5 PlanORN Agent

PlanORN represents the most comprehensive agent design evaluated in this study, combining explicit planning with note-based memory under a unified control structure. Although its time and token consumption are higher than those of the Basic agent in simple scenes, this overhead does not translate into a disadvantage as scene difficulty increases. On the contrary, PlanORN exhibits substantially higher stability and success rates in medium- and high-difficulty scenarios, where other agents experience rapid performance degradation.

In particular, as scene complexity increases, the performance of PlanORN degrades in a gradual and controlled manner rather than collapsing abruptly. This behavior indicates that explicit planning enables the agent to systematically reassess action sequences and memory validity when confronted with partial observability, delayed effects, or irreversible state changes. As a result, despite incurring additional planning overhead, PlanORN achieves more favorable Time-to-Success and Tokens-to-Success in challenging scenes, making it the most robust and well-balanced agent under the proposed benchmark.

## 5 Conclusion

Research on LLM-based agents has progressed rapidly, yet agent engineering remains challenging in practice, and agent designs are often described in highly heterogeneous and ad-hoc ways. This diversity of formulations reflects the complexity of agent systems, but it also introduces fundamental difficulties for both research and engineering. From an academic perspective, the growing engineering complexity of agents tends to obscure discussions of design principles and methodologies, allowing implementation details to leak into and confound higher-level analyses of agent behavior. From an engineering perspective, the design and improvement of agents are still largely heuristic, relying heavily on developers' experience and intuition rather than systematic and interpretable guidance.

Motivated by these challenges, the goal of this work is to identify an implementation-independent modeling perspective that captures the essential structure of LLM-based agents, and to build upon this perspective a sustainable and interpretable workflow for agent analysis and improvement. By decoupling agent design from specific implementations, we aim to support clearer methodological reasoning in research and more principled iteration in practice.

To address these challenges, we propose the Structural Context Model as a principled representation for describing LLM-based agents. In contrast to the ad-hoc and task-specific formulations commonly used in existing work, the model provides a clear boundary on what aspects of an agent are explicitly represented and what aspects are intentionally abstracted away. This explicit delineation allows agent designs to be discussed at the level of structure, rather than being entangled with implementation details or engineering artifacts.

By modeling agents as precise context structures and their transformations, the Structural Context Model offers a more direct and interpretable view of an agent's core mechanisms. The resulting representations are not only concise, but also amenable to formal analysis and comparison, enabling differences between agents to be examined in a systematic and implementation-independent manner. As a result, agent designs that may appear disparate under surface-level descriptions can be understood and contrasted within a unified analytical framework.

Under the Structural Context Model , agent behavior is expressed in terms of composable functions, referred to as context patterns. This functional view naturally supports composition and reuse, providing a principled basis for modular agent design. By abstracting agent components as reusable patterns rather than monolithic implementations, the model facilitates higher reusability of agent components, thereby improving development efficiency and accelerating design iteration.

Building upon this perspective, we further propose Semantic Dynamics Analysis as a methodology for extracting reusable context patterns from existing agent designs and for analyzing their roles and interactions. Rather than stopping at the decomposition and comparison of agent designs, this methodology leverages the descriptive and analytical capabilities of the Structural Context Model to study how context patterns contribute to agent behavior and how they relate to one another. Importantly, Semantic Dynamics Analysis serves as a bridge between formal analysis and practical engineering: it enables insights derived from structural analysis to feed back

into agent implementation through faster iteration, while also supporting a shift in agent research from heuristic, experience-driven study toward more data-driven and sustainable investigation.

We evaluate the proposed model and methodology in a set of variant monkey-banana problems and use them to guide the development of several agent designs. The benchmark provides a controlled yet challenging environment for studying agent behavior under increasing task difficulty. Through this process, we find that the proposed approach substantially improves development and iteration efficiency, enabling the rapid construction of agents that exhibit stronger robustness to difficulty scaling. In particular, the resulting agents maintain competitive overall engineering efficiency in medium and difficult scenarios, demonstrating the practical effectiveness of the proposed framework.

## 5.1 Limitation &amp; Future Work

While the Structural Context Model and Semantic Dynamics Analysis provide a principled foundation for analyzing and engineering LLM-based agents, several limitations remain and point to promising directions for future work.

1. The current formulation of Semantic Dynamics Analysis offers only a preliminary characterization of the geometric relationships among context patterns. Although initial propositions demonstrate the feasibility of analyzing and comparing patterns within the proposed framework, these results remain at an early stage. Stronger and more expressive propositions and lemmas are needed to more precisely characterize pattern properties and interactions. Developing such results will likely require analysis across a substantially larger and more diverse set of agent designs, enabling the refinement of definitions and the derivation of more general theoretical statements.
2. The degree of automation supported by the current Semantic Dynamics Analysis remains limited. At present, the identification and interpretation of context patterns still rely on significant human involvement. If stronger, more formally grounded propositions can be established to automatically detect and classify special properties of context patterns, it may become possible to deploy automated analysis systems at scale. Such systems could, for example, perform large-scale mining of agent implementations on public code repositories to discover recurring or structurally significant context patterns.
3. An open and speculative direction concerns the discovery of novel context patterns that are not yet widely used in existing agent designs. Beyond analyzing known patterns, Semantic Dynamics Analysis may offer a pathway to systematically uncover new patterns that are more efficient or effective with respect to specific objectives. Whether such patterns can be reliably discovered through data-driven structural analysis remains an open question and an intriguing avenue for future investigation.

Table A1 : Pairwise ∆Semantics Between Segments Under Different Orders

| ID       | Segment 2   | Segment 3   |   Segment 4 |
|----------|-------------|-------------|-------------|
| Segment1 | 0.3162      | 0.4347      |      0.5283 |
| Segment2 | -           | 0.4584      |      0.5536 |
| Segment3 | -           | -           |      0.3776 |

Taken together, these limitations highlight that the Structural Context Model and Semantic Dynamics Analysis currently remain at the level of formal representations and initial analytical tools. A natural next step is to further refine their definitions and introduce stricter and more expressive propositions, with the goal of developing a more complete algebraic system for reasoning about agent designs. Progress along this direction may help advance the study of LLM-based agents from experience-driven Agent Engineering toward a more data- and proof-driven Agent Science.

## Appendix A Detailed Analysis Results for the Task Decomposition Prompt

Figure A1 presents the complete semantic dynamics analysis results for the taskdecomposition prompt discussed in the main text.

Table A1 reports the pairwise ∆Semantics between segments under different ordering configurations, which is used to assess the degree of order invariance between segment pairs. From the results, the segment pairs (Segment 1, Segment 2), corresponding to the role settings and the goal settings, as well as (Segment 3, Segment 4), corresponding to the detailed steps and the output format, exhibit relatively strong order invariance.

## Appendix B Experiments Results on the Most Difficult Scenes

Table B2 presents the experimental results on the five most difficult scenes (ID 14, ID 3, ID 9, ID 12, ID 15).

## References

- [1] Aeronautiques, C., Howe, A., Knoblock, C., McDermott, I.D., Ram, A., Veloso, M., Weld, D., Sri, D.W., Barrett, A., Christianson, D., et al.: Pddl- the planning domain definition language. Technical Report, Tech. Rep. (1998)
- [2] Bacchus, F., Kabanza, F.: Using temporal logics to express search control knowledge for planning. Artificial intelligence 116 (1-2), 123-191 (2000)

Table B2 : Experimental results on the five most difficult scenes (ID 14, ID 3, ID 9, ID 12, ID 15). The table reports the success rate (SR. ↑ ), average number of steps (Avg. Steps ↓ ), average execution time (Avg. Time ↓ ), average token consumption (Avg. Tokens ↓ ), as well as the Time-to-Success (Time-to-S ↓ ) and Token-to-Success (Tokento-S ↓ ) metrics for each agent. Agents proposed in this work are marked with an asterisk (*), and the best result for each metric is underlined. Arrows ( ↑ / ↓ ) indicate whether higher or lower values are better, respectively. The symbol ' ∞ ' indicates that no successful trials were observed.

| Scene    | Agent    |   SR. ↑ |   Avg. Steps ↓ |   Avg. Time(s) ↓ | Avg. Tokens ↓   | Time-to-S ↓   | Tokens-to-S ↓   |
|----------|----------|---------|----------------|------------------|-----------------|---------------|-----------------|
| Scene 14 | Basic    |    0.03 |           21.6 |            36.91 | 132.4K          | 1230.25       | 7.0M            |
| Scene 14 | ReAct    |    0.68 |           71.8 |           856.34 | 2.9M            | 1259.33       | 1.5M            |
| Scene 14 | MLDT     |    0.12 |           18.4 |            43.4  | 110.2K          | 361.66        | 1.2M            |
| Scene 14 | OR*      |    0.78 |           40.7 |           133.28 | 579.1K          | 170.87        | 628.3K          |
| Scene 14 | ORN*     |    0.72 |           54.5 |           276.57 | 1.7M            | 384.13        | 1.7M            |
| Scene 14 | PlanORN* |    0.89 |           48   |           253.78 | 1.6M            | 285.15        | 1.3M            |
| Scene 3  | Basic    |    0    |           27.1 |            49.42 | 186.8K          | ∞             | ∞               |
| Scene 3  | ReAct    |    0.53 |           84.3 |          1066.64 | 4.5M            | 2012.53       | 910.9K          |
| Scene 3  | MLDT     |    0    |           28.1 |            63.27 | 239.0K          | ∞             | ∞               |
| Scene 3  | OR*      |    0.37 |           70.6 |           291.63 | 2.1M            | 788.18        | 4.6M            |
| Scene 3  | ORN*     |    0.39 |           72.9 |           423.08 | 3.0M            | 1084.82       | 7.0M            |
| Scene 3  | PlanORN* |    0.66 |           83.8 |           503.38 | 4.2M            | 762.69        | 3.2M            |
| Scene 9  | Basic    |    0    |           31.6 |            63.09 | 456.4K          | ∞             | ∞               |
| Scene 9  | ReAct    |    0.5  |          105.9 |          1641    | 7.0M            | 3251.04       | 1.9M            |
| Scene 9  | MLDT     |    0    |           21.9 |            53.16 | 172.7K          | ∞             | ∞               |
| Scene 9  | OR*      |    0.4  |           87.7 |           390.94 | 3.4M            | 977.36        | 4.5M            |
| Scene 9  | ORN*     |    0.32 |          133.7 |          1077.15 | 10.4M           | 3366.08       | 9.8M            |
| Scene 9  | PlanORN* |    0.56 |          109.7 |           829    | 9.0M            | 1480.35       | 6.3M            |
| Scene 12 | Basic    |    0    |           25.7 |            46.49 | 189.8K          | ∞             | ∞               |
| Scene 12 | ReAct    |    0.33 |          106.9 |          1541.9  | 5.5M            | 4671.04       | 7.0M            |
| Scene 12 | MLDT     |    0    |           26.6 |            62.15 | 213.3K          | ∞             | ∞               |
| Scene 12 | OR*      |    0.26 |           78.5 |           385.49 | 3.0M            | 1482.66       | 8.3M            |
| Scene 12 | ORN*     |    0.22 |           66.3 |           390.96 | 2.9M            | 1777.07       | 21.4M           |
| Scene 12 | PlanORN* |    0.5  |           99.6 |           726.11 | 6.8M            | 1452.21       | 10.2M           |
| Scene 15 | Basic    |    0    |           21.9 |            41.24 | 174.8K          | ∞             | ∞               |
| Scene 15 | ReAct    |    0.18 |          120.1 |          1880.3  | 7.4M            | 10446.09      | 16.3M           |
| Scene 15 | MLDT     |    0    |           23.3 |            60.95 | 235.8K          | ∞             | ∞               |
| Scene 15 | OR*      |    0.27 |          111.9 |           691.11 | 6.4M            | 2585.26       | 17.4M           |
| Scene 15 | ORN*     |    0.24 |          129   |          1077.71 | 10.9M           | 4490.45       | 30.4M           |
| Scene 15 | PlanORN* |    0.32 |          115.6 |           793.93 | 8.5M            | 2481.03       | 17.9M           |

- [3] Yao, S., Zhao, J., Yu, D., Du, N., Shafran, I., Narasimhan, K.R., Cao, Y.: React: Synergizing reasoning and acting in language models. In: The Eleventh International Conference on Learning Representations (2023). https://openreview.net/forum?id=WE vluYUL-X
- [4] Wu, Y., Zhang, J., Hu, N., Tang, L., Qi, G., Shao, J., Ren, J., Song, W.: MLDT: Multi-level decomposition for complex long-horizon robotic task planning with open-source large language model. In: Onizuka, M., Lee, J.-G., Tong, Y., Xiao, C., Ishikawa, Y., Amer-Yahia, S., Jagadish, H.V., Lu, K. (eds.) Database Systems for Advanced Applications, pp. 251-267. Springer
- [5] Zhang, A.L., Kraska, T., Khattab, O.: Recursive language models. arXiv preprint arXiv:2512.24601 (2025)

- [6] Silver, T., Hariprasad, V., Shuttleworth, R.S., Kumar, N., Lozano-P´ erez, T., Kaelbling, L.P.: Pddl planning with pretrained large language models. In: NeurIPS 2022 Foundation Models for Decision Making Workshop (2022)
- [7] Yao, W., Heinecke, S., Niebles, J.C., Liu, Z., Feng, Y., Xue, L., Murthy, R., Chen, Z., Zhang, J., Arpit, D., et al.: Retroformer: Retrospective large language agents with policy gradient optimization. arXiv preprint arXiv:2308.02151 (2023)
- [8] Shinn, N., Cassano, F., Gopinath, A., Narasimhan, K., Yao, S.: Reflexion: Language agents with verbal reinforcement learning. In: Oh, A., Naumann, T., Globerson, A., Saenko, K., Hardt, M., Levine, S. (eds.) Advances in Neural Information Processing Systems, vol. 36, pp. 8634-8652. Curran Associates, Inc. https://proceedings.neurips.cc/paper files/paper/2023/file/ 1b44b878bb782e6954cd888628510e90-Paper-Conference.pdf
- [9] Cai, T., Wang, X., Ma, T., Chen, X., Zhou, D.: Large language models as tool makers. arXiv preprint arXiv:2305.17126 (2023)
- [10] Yao, S., Yu, D., Zhao, J., Shafran, I., Griffiths, T., Cao, Y., Narasimhan, K.: Tree of thoughts: Deliberate problem solving with large language models. Advances in neural information processing systems 36 , 11809-11822 (2023)
- [11] Huang, W., Xia, F., Xiao, T., Chan, H., Liang, J., Florence, P., Zeng, A., Tompson, J., Mordatch, I., Chebotar, Y., et al.: Inner monologue: Embodied reasoning through planning with language models. arXiv preprint arXiv:2207.05608 (2022)
- [12] Mei, K., Zhu, X., Xu, W., Hua, W., Jin, M., Li, Z., Xu, S., Ye, R., Ge, Y., Zhang, Y.: Aios: Llm agent operating system. arXiv preprint arXiv:2403.16971 (2024)
- [13] Packer, C., Fang, V., Patil, S., Lin, K., Wooders, S., Gonzalez, J.: Memgpt: Towards llms as operating systems. (2023)
- [14] Pickel, B., Szab´ o, Z.G.: Compositionality. In: Zalta, E.N., Nodelman, U. (eds.) The Stanford Encyclopedia of Philosophy, Winter 2025 edn. Metaphysics Research Lab, Stanford University, ??? (2025)
- [15] Nouwen, R., Brasoveanu, A., Eijck, J., Visser, A.: Dynamic Semantics. In: Zalta, E.N., Nodelman, U. (eds.) The Stanford Encyclopedia of Philosophy, Fall 2022 edn. Metaphysics Research Lab, Stanford University, ??? (2022)
- [16] Vaswani, A., Shazeer, N., Parmar, N., Uszkoreit, J., Jones, L., Gomez, A.N., Kaiser, glyph[suppress] L., Polosukhin, I.: Attention is all you need. Advances in neural information processing systems 30 (2017)
- [17] Richmond, M.: The monkey and the bananas. Physics Education 1 (2), 138 (1966)
- [18] Hong, K., Troynikov, A., Huber, J.: Context rot: How increasing input tokens

impacts llm performance. URL https://research. trychroma. com/context-rot, retrieved October 20 , 2025 (2025)

- [19] Laban, P., Hayashi, H., Zhou, Y., Neville, J.: Llms get lost in multi-turn conversation. arXiv preprint arXiv:2505.06120 (2025)

SemanticDynamics

.

Fabric

<!-- image -->