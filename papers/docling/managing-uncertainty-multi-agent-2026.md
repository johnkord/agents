2

## Managing Uncertainty

## in LLM-based Multi-Agent System Operation

Man Zhang 1 , Tao Yue ∗ 1 , and Yihua He 2

1 Beihang University

Maternal-Fetal Medicine Center in Fetal Heart Disease, Capital Medical University, Beijing Anzhen Hospital

{ manzhang, yuetao } @buaa.edu.cn , heyihuaecho@hotmail.com

## Abstract

Applying LLM-based multi-agent software systems in safety-critical domains such as lifespan echocardiography introduces system-level risks that cannot be addressed by improving model accuracy alone. During system operation, beyond individual LLM behavior, uncertainty propagates through agent coordination, data pipelines, human-in-the-loop interaction, and runtime control logic. Yet existing work largely treats uncertainty at the model level rather than as a first-class software engineering concern. This paper approaches uncertainty from both system-level and runtime perspectives. We first differentiate epistemological and ontological uncertainties in the context of LLM-based multi-agent software system operation. Building on this foundation, we propose a lifecycle-based uncertainty management framework comprising four mechanisms: representation, identification, evolution, and adaptation. The uncertainty lifecycle governs how uncertainties emerge, transform, and are mitigated across architectural layers and execution phases, enabling structured runtime governance and controlled adaptation. We demonstrate the feasibility of the framework using a real-world LLM-based multi-agent echocardiographic software system developed in clinical collaboration, showing improved reliability and diagnosability in diagnostic reasoning. The proposed approach generalizes to other safety-critical LLM-based multi-agent software systems, supporting principled operational control and runtime assurance beyond model-centric methods.

Keywords : Uncertainty, Management, LLM-based Systems, Multi-Agent

## 1 Introduction

Large Language Model (LLM)-based multi-agent software systems represent a paradigm shift where LLMs serve as the central reasoning core for processing complex, multi-modal data [12, 8]. A prominent example is the development of LLM-basesd systems for lifespan echocardiography which integrate LLMs with cardiac imaging and clinical records to provide context-aware diagnostic reasoning that adapts to unique physiological changes from infancy through old age. Such applications leverage foundation models (e.g., DeepSeek [17] and GPT-4 [1]) to interpret high-dimensional data, but introduce systemic complexities that traditional architectures were not designed to handle.

In high-stakes domains such as disease diagnosis, managing uncertainties is fundamental for clinical safety [25], long before LLMs became available. LLM-based multi-agent systems are uniquely susceptible to hallucinations, where plausible but factually incorrect diagnostic justifications are generated, as well as inherent uncertainties in the operating environment and sensor noise [32]. Furthermore, out-of-distribution clinical phenotypes (e.g., heterotaxy with situs ambiguous) or shifts in the deployment infrastructure (e.g., microservice scaling) create non-deterministic behaviours. Without rigorous operational bounding, these fluctuations can lead to potentially catastrophic in time-critical cardiac assessments.

Existing uncertainty-aware software engineering research [38, 11, 39, 27, 33, 3, 7] provides a strong foundation, but it must be updated to address the distinct challenges of LLM-based multi-agent software systems. Established methodologies for uncertainty-aware specification and analysis [41, 23, 35, 11], uncertainty modeling [39, 2, 4, 20], uncertainty-aware testing [36, 37, 6], and uncertainty-aware simulation [13] were largely proposed for systems with well-defined operational boundaries and explicit logic paths. With the rise of LLMs, a growing body of work has emerged to study uncertainties inherent to the models themselves, primarily focusing on uncertainty quantification for token generation and confidence calibration [18, 24], scenario-independent uncertainty estimation [31] to

∗ Corresponding author

disentangle semantic truth from lexical noise, the detection of internal hallucinations through self-probing and consistency check [21], etc. However, there is currently a lack of systematic research that addresses uncertainty from the perspective of LLM-based multi-agent software systems, where the LLM serves as a reasoning core integrated with possibly multi-modal data, dynamic microservices, and human-in-the-loop (HITL) workflows. In domains like lifespan echocardiography, managing uncertainty requires concerning both model-level metrics, system-wide infrastructure, and the human-machine interface (HMI). This multi-layered perspective ensures that risks are not only detected within the computational core but also effectively communicated and mitigated through collaborative workflows between the autonomous agents and human experts.

In this paper, we aim to systematically examine the diverse types of uncertainty inherent in LLM-based multi-agent software systems, illustrating our findings through a real-world industrial case study of an LLM-based echocardiographic platform. Based on these understandings, we propose a strategic vision for the comprehensive management of uncertainty in such systems. We specifically draw attention to the recently published Precise Semantics for Uncertainty Modeling (PSUM) international standard [22] standardized at the Object Management Group (OMG), which was built on years of effort of the community, especially the uncertainty modeling conceptual model: U-Model [40]. We argue that while PSUM was defined prior to the widespread adoption of LLMs, its belief-centered approach remains highly relevant for characterizing various types of uncertainties in LLM-based multi-agent software systems. Our long term is to leverage PSUM to develop a systematic infrastructure that enables the active management of diverse uncertainties throughout their lifecycles.

Specifically, PSUM [22] aims to make uncertainty explicit within a defined model by attaching it to BeliefStatement s (tangible representations of a BeliefAgent 's knowledge) through UncertaintyTopic . The PSUM metamodel characterizes Belief , IndeterminacySource , Uncertainty , and define relationships among them and associate them to Evidence and Risk . Especially, rather than treating uncertainty purely as a numerical attribute, PSUM models uncertainty as a belief-centered construct grounded in identifiable causes of indeterminacy. PSUM also provides the way of quantifying uncertainty and other MeasurableElement , such that numerical or symbolic values can be assigned to features such as Accuracy or Precision .

We envision an uncertainty-aware framework for managing the evolving uncertainty inherent in LLM-based multi-agent systems during their operation. Given the complexity of autonomous agents, heterogeneous data sources, and intricate inter-agent interactions, our framework adopts a systematic uncertainty lifecycle approach, which transitions through states such as Detected , Characterized , Mitigated , and Resolved . This lifecycle is governed by four core mechanisms ( Representation , Identification , Evolution , and Adaptation ). By aligning these mechanisms, in a long term, we aim to achieve three critical objectives: the explicit representation of uncertainty, its continuous and progressive handling, and adaptable system operation under uncertainty. Central to our vision is the elevation of human actors' roles to uncertainty-aware agents who participate selectively and accountably within the uncertainty lifecycle, to ensure that human-machine collaboration is both transparent and grounded in the system's current state of belief.

The rest of this paper is organized as follows: Section 2 provides a systematic classification of uncertainties within the context of LLM-based multi-agent systems. Section 3 introduces our vision for an uncertainty management framework, detailing its core mechanisms and the proposed uncertainty lifecycle. Finally, Section 4 concludes the paper and outlines directions for future research.

## 2 Understanding Uncertainties

The inherent complexity of LLM-based multi-agent software systems (e.g., LLM-based systems for lifespan echocardiography) necessitates a careful understanding of the diverse forms of uncertainty. By identifying these distinct types, it becomes possible to move beyond treating the system as an opaque ''black box'' and instead develop targeted methodologies to manage these uncertainties, which span ontological origins, learning dynamics, model behaviour, and decision-making impact.

We classify such uncertainties into: epistemological uncertainty (EpiU) from ontological uncertainty (OU), as illustrated in Figure 1. Epistemological uncertainty arises from limited, imperfect, or abstracted knowledge about a system, its data, or its models, and has long been recognized as a fundamental source of uncertainty in modelling and decision-making [28, 10]. It is in principle reducible with better models, data, or reasoning. This type of uncertainty is essentially about what we know and how we represent what we know, not in the system itself. Hence, it is knowledge-based . Ontological uncertainty arises from inherent properties of the system or environment, is independent of our knowledge, and is irreducible even with complete information, and has been discussed extensively in philosophical and systems literature [16, 15]. It is in principle about uncertainty in the system (hence reality-based ), not in our description of the system. The reality's complexity forces the observers to be ignorant (''OU causes EpiU''); one cannot know a result more precisely than the source's inherent indeterminacy allows, which effectively limits the reduction of uncertainty (''OU limits EpiU reduction''). Over time, ontological events may transit into EpiU as they move from potential (future) occurrences to (already-occurred) historical facts that may be poorly recorded or understood (''OU becomes EpiU over time'').

Figure 1: Overview of Epistemological Uncertainty, Ontological Uncertainty, PSUM, and their Relations.

<!-- image -->

Figure 2: Epistemological Uncertainty

<!-- image -->

Understanding the relations helps to systematically management uncertainties.

## 2.1 Epistemological Uncertainty

As shown in Figure 2, the four epistemological uncertainty types are conceptually orthogonal.

## 2.1.1 Model Uncertainty

In general, a model is an abstract representation of phenomenon or knowledge domains constructed to support reasoning, prediction, explanation, or decision-making, by omitting detail and idealizing reality. Since every model is built via abstraction, such as selecting scope, granularity, assumptions, and representational forms, which inevitably introduces epistemological uncertainty. This also reflects the famous statement by George Box: ''all models are wrong, but some are useful'' [5]. Taking LLMs as example, they can never perfectly replicate the real world, but they are incredibly useful by finding patterns that help experts make better decisions such as echocardiographic diagnosis.

We distinguish four types of model uncertainty: structural, behavioural, parameter, and semantic, which all arise from abstraction choices made during modelling, and together shape the adequacy of the model representation. In contrast, applicability uncertainty concerns whether these abstractions remain appropriate for a given application context.

Structural uncertainty in operation concerns the dynamic selection and orchestration of the system's reasoning components and their relationships. For instance, as described in [30], adaptive planning architectures allow these systems to adjust decision-making frameworks at runtime based on task complexity. In such architectures, the planning layer continuously updates clinical strategies, prioritizes urgent cases, and refines previous decisions as findings emerge, to enable specialized responses to diverse patient needs, adapt to varying task complexities and fluctuating computational resources, and integrate real-time evidence, which inevitably introduces structural uncertainty during system operation.

Behavioural uncertainty in operation concerns the non-deterministic variability in a system's internal execution logic and process-level transitions. As evidenced in adaptive medical agent frameworks [30], this uncertainty is driven by the stochastic nature of the reasoning and planning layers, where the system may follow divergent reasoning traces or trigger variable self-correction loops for identical inputs. Notably, behavioural uncertainty frequently serves as a primary driver of inferential uncertainty (see Section 2.1.3); the instability in the derivation process (how the conclusion is reached) directly leads to the doubt in the final prediction or decision (what is concluded).

Parameter uncertainty in operation concerns uncertainty in numerical or symbolic values that control model behaviour at runtime. This goes beyond static weights of a trained model and include configurable thresholds for task complexity, priority scoring within the adaptive planning layer of an LLM-based multi-agent systems.

Semantic uncertainty concerns about the real-world meaning and interpretation of model constructs, such as uncertainty in the correspondence between model elements and domain concepts, uncertainty about how learned internal representations in models correspond to intended domain semantics. This uncertainty manifests in multi-agent orchestration, where specialized agent roles may lack a shared semantic grounding, leading to misinterpretations of perception data and misaligned reasoning traces.

Applicability uncertainty concerns if a model's internal abstractions and foundational assumptions remain valid when the system is applied beyond its intended operational envelope.

## Model Uncertainty - Examples

Structural Uncertainty. The system's adaptive planning layer decides at runtime whether to orchestrate a singleagent diagnostic trace for a routine case or a collaborative expert group (consisting of specialized pediatric, surgical, and hemodynamic agents) to interpret complex congenital heart defects.

Behavioural Uncertainty. Inconsistent task sequencing or reasoning paths in an echocardiography agent can lead to poorly calibrated confidence scores, as the underlying derivation procedure lacks a stable, reproducible execution logic.

Parameter uncertainty. An LLM-based agent may assign varying weights to clinical markers (e.g., prioritizing certain echo measurements over others) based on the past cases. Any operational miscalibration in these dynamic weights can lead to biased diagnostic inference.

Semantic uncertainty. A general triage agent might interpret a heart rate of 140 bpm as a''critical'' emergency, while a pediatric agent interprets the same value as 'normal' for a neonate. This discrepancy in meaning can lead to misaligned reasoning traces and incorrect clinical planning.

Applicability uncertainty. Applying the system trained on high-resolution adult scans to handheld pediatric POCUS introduces applicability uncertainty, as the model's learned abstractions regarding image quality may not generalize.

## 2.1.2 Data Uncertainty

Data uncertainty in operation concerns the quality, reliability, and representativeness of the observations or datasets used to execute or evolve models. This includes noise in input signals, incomplete data records, or distributional instability where the operational data significantly drifts from the system's initial training distribution [34]. Within a multi-agent framework, when using uncertain data to evolve the model, data uncertainty fundamentally alters the system's future reasoning logic and parameters; when using uncertain data solely to operate a fixed agent, the uncertainty propagates to the output without altering the underlying model representation. We classify data uncertainty into four categories: noise uncertainty, missing data, sample bias, and distributional shift, because together they capture the fundamental ways in which data can be epistemically imperfect.

Noise uncertainty arises from incomplete knowledge, imperfect characterization, or the simplified modeling of perturbations affecting observed data. In LLM-based systems, this is not limited to physical sensor noise; it critically includes semantic noise (ambiguity, subjectivity, or inconsistency in how data values or labels are defined and assigned). When agents operate on such ''noisy'' inputs, the lack of a precise ground truth can trigger divergent reasoning traces or unnecessary reflection loops, as the system struggles to distinguish between meaningful signals and artifacts of imperfect data characterization.

Missing data is caused by the absence of values for some variables or instances, which consequently restricts the understanding about the modelled phenomenon.

Sampling bias in operation refers to the epistemic limitation where the subset of information actively used or retrieved by the system is not representative of the application context. This leads to a contextual mismatch where a representative reasoning trace is impossible because the agent is operating on a skewed fragment of the available truth.

Distributional shift arises when the statistical properties of the data used during model construction differ from those encountered during model application.

## Data Uncertainty - Examples

Noise uncertainty. In LLM-based lifespan echocardiography, noise uncertainty arises from unmodeled physical perturbations (e.g., patient respiratory cycles) and semantic inconsistencies (e.g., subjective expert labeling), which introduce variability into the diagnostic data.

Missing data. The absence of specific Doppler flow measurements or obscured anatomical views in neonatal scans prevents an agent from satisfying the logical preconditions required to transition from raw observation to a finalized clinical plan, often forcing the system to request human clarification.

Sampling bias. The system introduces uncertainty through the biased selection of evidence from its experience base or the incomplete sampling of patient features during multi-agent handoffs, i.e., the loss of critical information when one agent summarizes or filters a patient's data before passing it to the next agent.

Distributional shift. If a medical facility adopts a new ultrasound device that produces measurements with a higher mean value than those in the LLM's training set, in operation, the triage agent may over-report ''abnormal'' findings because its internal thresholds are no longer calibrated to the new data distribution, potentially triggering unnecessary and costly collaborative expert group sessions for healthy patients.

## 2.1.3 Inferential Uncertainty

Inferential uncertainty is about uncertainty introduced during the process of deriving conclusions, making predictions or decisions from a given model and data. Such uncertainties reflect limitations in inference, estimation, or decision procedures rather than in the model structure or data themselves. Inferential uncertainty can be commonly classified into prediction uncertainty and calibration uncertainty.

Prediction uncertainty refers to the quantified doubt in the model's generated output for a specific input set. Calibration uncertainty arises when a confidence score becomes an unreliable reflection of reality, often due to biased or outdated factors such as a stale dataset or a significant change in the operating context.

## Inferential Uncertainty - Examples

Prediction uncertainty. When evaluating a neonatal echocardiogram with missing Doppler flows, the system may output a diagnosis of ''suspected Patent Ductus Arteriosus (PDA)'' with a belief score of 0.72. This prediction uncertainty signals to the clinician that the lack of complete physiological data prevented the system from reaching the high-confidence threshold required for a definitive diagnostic claim.

Calibration uncertainty. If a model trained on adult data provides a 95% confidence score for a pediatric diagnosis, that score may be poorly calibrated and thus an unreliable reflection of true accuracy. This requires a meta-level evaluation of whether the assigned probability can be trusted as a valid representation of the system's diagnostic competence in a specific clinical context.

## 2.1.4 Interpretational Uncertainty

It concerns uncertainty about how results should be understood, communicated, or acted upon by humans, organizations and automated agents. It is not uncertainty in the model, data, inference outcomes or confidence values, and comes after the inference is complete. Interpretational uncertainty can be decomposed into semantic ambiguity , explanation uncertainty , and interpretation variance across belief agents, capturing uncertainty in meaning (e.g., vague labels), justification (e.g., why a specific result is produced), and agent-dependent understanding of results (e.g., different agents interpreting results differently).

More specifically, semantic ambiguity is about the meaning of model outputs, concepts, or labels, arising when their semantics are underspecified, overloaded, or context-dependent. Explanation uncertainty is about the adequacy, faithfulness, or completeness of explanations provided for model results, regardless of whether the explanations are causal or non-causal. Interpretation variance arises when different belief agents draw different conclusions or actions from the same result.

## Interpretational Uncertainty - Examples

Semantic ambiguity. A diagnostic output such as ''increased chamber size'' has multiple interpretations depending on the patient's age, making the clinical significance of the AI's finding unclear without further context.

Explanation uncertainty. While an LLM may correctly diagnose a congenital heart defect, the accompanying explanation (e.g., citing specific flow velocities) may be unfaithful to the actual data processed or incomplete in its clinical reasoning, which leaves the cardiologist uncertain as to if the LLM's conclusion is grounded in trustworthy evidence.

Interpretation variance. An LLM and cardiologists derive different clinical actions from the same diagnostic report.

## 2.2 Ontological Uncertainty

As shown in Figure 3, we classify ontological uncertainties into three categories. Aleatory uncertainty is nature-driven , as its irreducible randomness originates from physical stochasticity inherent in the world, such as unavoidable signal noise in ultrasound physics or environmental variability during an echocardiographic scan.

Figure 3: Ontological Uncertainty

<!-- image -->

Architectural morphing is system-driven. In the context of LLM-based multi-agent software systems, it focuses on the underlying infrastructure of the applications, which may arise from runtime changes in the system's own structure, such as the dynamic scaling of microservices, updates to the deployment pipeline, or the evolving internal decision logic of the LLM as it adapts to new data. Interaction uncertainty is agent-driven, stemming from the decentralized and interdependent decision-making between the system and its operators. For instance, in human-in-the-loop scenarios, the mutual influences and feedback loops between the cardiologist and the AI lead to collaborative outcomes that cannot be deterministically predicted from the actions of either agent in isolation.

## 2.2.1 Aleatory Uncertainty

It refers to irreducible randomness inherent in the behaviour of a system or its operating environment. It has been intensively discussed in AI [26, 29]. The key property is that aleatory uncertainty comes from randomness in physical or computational processes; hence, even with perfect monitoring or control, outcomes remain stochastic and hence irreducible in principle or by definition.

## 2.2.2 Architectural Morphing

With architectural morphing, we aim to describe the runtime evolution of a system's internal structure, its configuration and composition during execution . Its key property is emergent change in the sense that the system's topology or behaviour arises at runtime. Note that the adaptation logic might be pre-defined, but a specific configuration is triggered by shifting environmental configurations or evolving learning objectives. This is prevalent in self-adaptive systems that autonomously reconfigure their architectures such as dynamically swapping or scaling microservices [19] and in learning-enabled systems where an AI agent's internal decision logic evolves through continuous reinforcement learning [14].

Architectural morphing is different from aleatory uncertainty, regarding irreducibility. Aleatory uncertainty is irreducible in principle ; but architectural morphing is irreducible operationally in the sense that, even with perfect knowledge, the runtime structure changes of a system are driven by system adaptation mechanisms, which cannot be fully resolved at design time.

## 2.2.3 Interaction Uncertainty

It originates at the socio-technical boundary where a system interacts with humans, organizations, or other AI agents, whose behaviours are strategic and uncontrollable. The key property of interaction uncertainty is autonomous interplay , where outcomes result from decentralized decision-making of belief agents rather than a single centralized logic. We see three different types of interaction dynamics: 1) reciprocal feedback loops, where system actions trigger adaptive responses of agents; 2) strategic interdependence, where outcomes (e.g., in LLM-enabled multi-agent systems [9]) depend on agents reacting to each other's actions or outputs; and 3) decentralization coordination, which arises when no single agent has a global view or control of the whole system, so its overall behaviour are resulted from many local decisions, which eventually makes the system's

behaviour hard to predict or explain. Hence, interaction uncertainty exhibits strategic irreducibility, as system behaviour emerges from mutually adaptive decisions among agents and cannot be decomposed into independent or centrally analyzable behaviours.

## Ontological Uncertainty - Examples

Aleatory uncertainty. The inherent acoustic speckle and thermal noise in ultrasound physics is aleatory uncertainty. Architectural morphing. It might be the dynamic scaling of microservices, or adaptive offloading of computational tasks between edge and cloud environment, or switching between adult and pediatric models at runtime.

Interaction uncertainty. It often stems from non-deterministic and decentralized collaboration between the cardiologist and the LLM-based agents, where the final clinical decision emerges from iterative feedback loops and competing agencies that cannot be predicted by analyzing either agent in isolation.

## 3 Uncertainty Management Framework

We propose an uncertainty-aware framework for managing uncertainty in LLM-based multi-agent software systems during operation. Such systems typically involve multiple autonomous agents, heterogeneous data sources, and complex inter-agent interactions, which subsequently introduce evolving forms of uncertainty in their operation. To address these challenges, our framework manages uncertainty via four mechanisms (i.e., Representation , Identification , Evolution and Adaptation ) that span the entire uncertainty lifecycle . These mechanisms are realized via role-based multi-agents (as shown in Figure 4), where uncertainty is perceived, analyzed, evolved, and acted upon in a coordinated and policy-governed manner, aligned with three key objectives:

Objective 1: Explicit uncertainty representation. Uncertainty is manageable only if it and its propagation are explicitly represented or annotated, rather than remaining implicit. Therefore, our first objective is to explicitly represent and characterize uncertainty, especially epistemological uncertainty, arising during system operation, such as data, models, reasoning, and agent interactions. Note that epistemological uncertainty can often be explicitly represented and progressively reduced through additional evidence, deliberation, or verification, while ontological uncertainty may not be fully eliminable or explicitly resolvable, as we discussed in Section 2.

Objective 2: Continuous and progressive uncertainty handling. As LLM-based agents observe new information, interact with one another, and incorporate human input, uncertainty may increase, decrease, or persist. Managing uncertainty needs to be an evolving process rather than a one-time handling. Our framework aims at systematically identifying both epistemological and ontological uncertainty as an LLM-based multi-agents system operates.

Objective 3: Adaptable system operation under uncertainty. To ensure that an LLM-based multiagent system continues to function properly in the presence of uncertainty, according to its severity and associated risk, our framework aims to safeguard system operation by adapting autonomy levels, coordination strategies, and decision-making behaviour. This enables bounded, and risk-aware system behaviour in operation. Unlike approaches that assume complete or accurate operational information, an assumption that is rarely practical, our framework explicitly treats uncertainty as a first-class concern.

## 3.1 Uncertainty Lifecycle

In the context of our uncertainty management framework, we define the lifecycle of each uncertainty U is composed of six states, i.e., Detected , Characterized , Mitigated , Resolved , Escalated , and Expired , governed by the identification, representation, evolution, and adaptation mechanisms. As illustrated in Figure 4, transitions between states are driven by time-indexed evidence E ( t ) and uncertainty-handling actions H ( u ), reflecting the dynamic nature of uncertainty during system operation within our management.

Detected. An uncertainty enters the lifecycle in the detected state when it is first detected. Detection may result from incomplete, unstable, conflicting, or ambiguous information observed or reasoned during the system operation. At this stage, the uncertainty is recognized without yet establishing its characteristics.

Characterized. In the Characterized state, the detected uncertainty is formally analyzed and represented. Its type, scope, severity, confidence, associated evidence and risk are determined and captured, which provides a structured basis for subsequent handling and decision-making.

Mitigated. The Mitigated state represents active uncertainty handling. Based on available evidence E ( t ), the framework applies mitigation actions, such as data acquisition, multi-agent reasoning, verification, or clarification, to reduce or bound the uncertainty. The uncertainty may remain in this state while mitigation continues or as new evidence is incorporated.

Resolved. An uncertainty transitions to the Resolved state when accumulated evidence E ( t ) sufficiently reduces its severity and associated risk, allowing normal system operation to proceed without additional safeguards.

Expired. The Expired state indicates that a decision has been taken despite residual uncertainty. Uncertainty is explicitly accepted and recorded, linking it to the committed decision to ensure traceability and accountability.

Figure 4: Overview of Uncertainty Management Framework

<!-- image -->

Escalated. Based on accumulated evidence E ( t ), uncertainty may transit from the Mitigated state to the Escalated state when associated risk remains high or cannot be adequately bounded through automated handling. In such cases, uncertainty-handling actions H ( u ) transfer decision authority to higher-level agents or human operators for judgment, oversight, or governance.

## 3.2 Uncertainty Representation

How to represent uncertainty. We propose to represent uncertainty with the PSUM international standard [22], as it provides a formally defined semantic foundation for modeling uncertainty, evidence, and risk. PSUM enables uncertainty to be expressed in a machine-interpretable and interoperable manner, making it suitable for coordination among multi-agent systems and deployment in critical domains such as healthcare.

In our framework, uncertainty is represented as a time-indexed PSUM-compliant object associated with all operational artifacts. These artifacts include system inputs, intermediate reasoning artifacts, decisions, and executed actions, reflecting the fact that uncertainty may arise and evolve throughout the entire reasoning and execution process in multi-agent context. With PSUM, an uncertainty instance can be formally modeled as:

<!-- formula-not-decoded -->

where type denotes an uncertainty category (e.g., data, model, inferential), scope defines the affected system components or decisions, O ( t ) captures associated ontological uncertainty that reflects irreducible variability or environmental indeterminacy, P ( t ) captures provenance and validity over time, E ( t ) denotes observed evidence or reasoned info, c ( t ) represents confidence or belief state, R ( t ) denotes associated operational risk, and τ defines temporal validity or expiration that governs state transitions in the uncertainty lifecycle. To represent interdependencies and propagation among uncertainties in a multi-agent context, U ↑ ( t ) denotes the set of upstream uncertainties on which the current uncertainty depends, such as uncertainties originating from

upstream agents, data sources, or prior reasoning steps. U ↓ ( t ) denotes the set of downstream uncertainties that are influenced or induced by the current uncertainty, capturing how uncertainty propagates across agents, tasks, and decision pipelines.

Uncertainty is inherently dynamic and evolving: as new evidence is observed, uncertainty may increase or decrease, prior assumptions may be invalidated, and associated risk may change. By explicitly modeling uncertainty as a lifecycle-aware and time-indexed object, this representation enables uncertainty to be systematically queried, characterized, propagated, updated, and audited across its identification, characterization, mitigation, escalation, and resolution states during system operation.

## 3.3 Uncertainty Identification

How to detect and characterize uncertainty. Uncertainty is identified through continuous detection mechanisms operating across multiple layers of the system, together with representation mechanisms that characterize the identified uncertainty. This process is primarily carried out by the Observer , Reasoner , and Constructor .

The Observer continuously perceives operational data from the environment and system state, while the Reasoner analyzes observations and intermediate reasoning results to accumulate time-indexed evidence E ( t ). Based on this evidence, the Constructor generates uncertainty instances by detecting their presence and formally characterizes them using PSUM-compliant representations.

For example, at the data level, uncertainty is identified by the Observer through validity checks, completeness constraints, cross-source inconsistency detection, and distributional shift indicators. At the reasoning level, the Reasoner analyzes inference results produced by LLM-based agents, which are required to emit calibrated confidence estimates and explicit evidence links; divergence among independently reasoning agents, unsupported conclusions, or missing mandatory tool invocations trigger inferential or model uncertainty. At the interaction level, ambiguity detectors, schema validation, concurrency conflicts, and feedback-loop patterns identify interpretational and interaction uncertainty.

In addition, higher-level operational indicators, such as frequent human overrides, repeated escalations, or persistent unresolved discrepancies, are treated as evidence of latent or systemic uncertainty. All identified uncertainty is explicitly instantiated, recorded in the shared uncertainty registry, and explicitly linked to the affected agents, artifacts, and decisions to support subsequent evolution and adaptation.

## 3.4 Uncertainty Evolution

How uncertainty evolves based on accumulated evidence. Uncertainty evolution describes how uncertainty changes over time once it has been identified and characterized . Rather than treating uncertainty as static, the framework models uncertainty as a dynamic object whose property may improve, persist, or deteriorate as new info becomes available.

The framework distinguishes epistemological uncertainty, which can often be reduced through additional evidence and actions, from ontological uncertainty, which cannot be eliminated and must instead be bounded and managed. Reduction of uncertainty is managed by two mechanisms, i.e., evolution and adaptation . Evolution mechanisms are designed for reducing or refining uncertainty based on info gathering and evidential analysis that do not directly influence system behavior, while actions or strategies that modify system execution for the purpose of uncertainty reduction are handled by adaptation mechanisms.

Uncertainty evolution is driven by coordinated interactions among the Observer , Reasoner , and Evolver . The Observer continuously monitors the environment and system state, collecting new observations and contextual info. The Reasoner analyzes these observations and intermediate reasoning results, integrating new info, reassessing prior assumptions, and updating the accumulated evidence E ( t ). Note that, during the evolution phase, human actors exercising external authority may judge that an uncertainty can be resolved or explicitly expired based on their expertise. Because such human interventions are neither routed nor orchestrated by the framework, they are incorporated solely as additional human-provided evidence E ( t ). Based on the evolving evidence, the Evolver revises the uncertainty state by updating its severity, confidence, and position within the uncertainty lifecycle. Uncertainty evolution is modeled as an event-driven state transition:

<!-- formula-not-decoded -->

where e represents events in which new evidence is acquired or becomes available, such as newly observed data, agent reasoning outputs, human-provided evidence, or time-based expiration.

Following characterization, uncertainty transitions from the Characterized state to the Mitigated state when evidential analysis motivates the initiation of uncertainty reduction or bounding activities. As additional evidence accumulates, uncertainty in the Mitigated state may transition to the Resolved state when its severity and associated risk is acceptable (e.g., based on policy-defined thresholds), or to the Expired state when a bounded decision is taken despite residual uncertainty. However, uncertainty may transition from the Mitigated state to the Escalated state when accumulated evidence indicates an increase in uncertainty level or when the

associated risk remains high. Conversely, uncertainty in the Escalated state may transition back to the Mitigated state when additional evidence enables further refinement or reduction of the uncertainty.

All evolution steps are explicitly recorded in the uncertainty registry, ensuring traceability of how uncertainty changes over time and how evidence contributes to lifecycle transitions.

## 3.5 Uncertainty Adaptation

How to safeguard system operation via uncertainty-aware adaptation. In contrast to evolution , which focuses on the continuous and explicit capture of uncertainty based on accumulated evidence, adaptation mechanisms are designed to safeguard system operation by regulating actions according to uncertainty severity and associated risk. Adaptation governs how the system adjusts its behavior, autonomy, and decision pathways in the presence of evolving uncertainty.

Adaptation is realized by coordinating the Observer , Reasoner , Orchestrator , and Commander . The Observer and Reasoner continuously provide updated evidence regarding system state, agent reasoning outcomes, and uncertainty evolution. This evidence forms the basis for adaptive decision-making.

The Orchestrator evaluates uncertainty states and accumulated evidence under policy and risk constraints, and determines which adaptive actions should be taken, including autonomy adjustment, verification requirements, workflow restructuring, or escalation. The Commander is responsible for executing the selected actions and enforcing system-level constraints, ensuring that adaptations are carried out in a controlled and auditable manner. When necessary, the Orchestrator may also notify relevant human operators and transfer decision authority to support judgment, oversight, and governance.

Adaptation operates in close coordination with planning, reasoning and coordination components in multiagent systems and depends on the degree of autonomous authority granted to the framework. The scope of adaptive actions is therefore determined by system policies that specify which aspects of execution the framework is permitted to modify. For instance, at the planning phrase, when the framework is authorized to modify execution strategies, it may adapt plans by switching between sequential execution and collaborative multi-agent deliberation upon detecting inferential uncertainty or agent disagreement. Planned actions may be deferred, decomposed into smaller reversible steps, or reformulated to explicitly surface unresolved uncertainty. Regarding reasoning, adaptation regulates how agents produce and validate conclusions under uncertainty. For example, the orchestrator may require additional evidence links, mandate tool-based verification, invoke specialist or critic agents, increase deliberation depth, or trigger parallel independent reasoning to quantify disagreement. When uncertainty remains high, the orchestrator may constrain permissible inferences, request clarification, or escalate to human review. For interactions among agents, adaptation addresses uncertainty arising from coordination and inter-agent dynamics rather than from individual agent reasoning. When interaction uncertainty is detected, such as conflicting actions, incompatible assumptions, concurrency conflicts, or unstable feedback loops, the orchestrator adapts how agents interact, rather than what they reason about. This includes enforcing coordination constraints, introducing arbitration mechanisms, limiting concurrency or execution rates, and restructuring interaction patterns to ensure consistent and stable collective behavior. Through these mechanisms, adaptation prevents local uncertainties from propagating or amplifying across agents, thereby safeguarding system operation.

Human actors are actively involved in adaptation mechanisms, participating as decision-makers by reviewing uncertainty annotations, supporting evidence, and potential consequences, and by guiding or authorizing adaptive actions. Human interventions are treated as first-class adaptation decisions.

## 3.6 Uncertainty Management with HITL

In complex, open-ended, and safety-critical environments (e.g., multi-agent systems), uncertainty may involve ambiguous intent, conflicting evidence, ethical considerations, or high-impact decisions that may not be handled properly by automated reasoning alone. Therefore, we incorporate HITL into our uncertainty management framework, enabling human participation across multiple mechanisms, such as uncertainty evolution and adaptation, to support such as judgment, oversight, and accountable decision-making. Our framework treats humans as uncertainty-aware agents , thereby ensuring robust, accountable, and risk-aware system operation under real-world uncertainty.

Human roles in uncertainty management. Humans play several roles in uncertainty management, with four primary roles contributing at distinct points in the uncertainty lifecycle: (1) Interpretation: resolving semantic ambiguity, intent uncertainty, and context-dependent meaning that automated agents cannot reliably disambiguate; (2) Judgment: weighing competing hypotheses when inferential uncertainty persists despite deliberation and verification; (3) Risk acceptance: explicitly accepting residual uncertainty for high-impact or irreversible decisions; and (4) Governance: providing accountability, ethical oversight, and compliance validation. These roles align human involvement with specific uncertainty states and transitions, rather than ad hoc intervention.

Uncertainty-driven human engagement. Human involvement is not constant or regular, but triggered by policy-defined uncertainty concerns within the orchestrator or as part of a business scenario. Specifically, human engagement is initiated when uncertainty severity or associated risk exceeds the reliable scope of automated handling, such as in situations involving persistent disagreement among expert agents, unresolved high-severity data gaps, or decisions with significant safety, legal, or ethical implications. This choice ensures that human expertise is applied only when necessary, while minimizing unnecessary intervention in low-risk or well-bounded decisions.

HMI for uncertainty-aware interaction. When engaging humans, the system is required to present uncertainty via a dedicated HMI. The interface provides a structured view of the current decision or recommendation, the set of unresolved uncertainties, the supporting and conflicting evidence, the assumptions made by the system, and the potential consequences of action or inaction. This presentation enables humans to resolve, contextualize, or explicitly accept uncertainty with relevance and awareness, instead of being asked to simply approve or reject outputs.

Human actions in uncertainty evolution and adaptation. In our framework, human inputs are treated as first-class events that directly participate in multiple uncertainty management mechanisms. Human actions contribute to uncertainty evolution by refining, reducing, or accepting uncertainty through clarification, expert judgment, or explicit risk acknowledgment, which are incorporated as human-provided evidence E ( t ). They also contribute to uncertainty adaptation by guiding or authorizing changes in system behavior, represented as uncertainty-handling actions H ( u ), when automated handling is insufficient. In addition, human interventions may introduce new uncertainty that must be explicitly detected and subsequently characterized and managed by the framework. All such actions are recorded in the uncertainty registry and linked to the affected uncertainty instances and decisions, preserving traceability and accountability across the entire uncertainty lifecycle.

## 4 Conclusion and Future Work

As LLM-based multi-agent systems are transitioning from experimental prototypes to mission-critical deployments like lifespan echocardiography, the ability to systematically manage uncertainty becomes a foundational requirement for clinical trust and software system reliability. This paper has moved beyond viewing uncertainty as a mere statistical error, reframing it as a dynamic lifecycle challenge inherent to the software engineering of autonomous systems. By establishing a rigorous understanding of epistemological and ontological uncertainties and proposing a framework grounded in uncertainty representation, identification, evolution, and adaptation, we provide a blueprint for systems that do not just experience uncertainty but actively reason about it and hence manage it systematically.

To ground this approach in established software standards, we examined the feasibility of applying the international standard Precise Semantics for Uncertainty Modeling (PSUM) as the formal foundation for representing these uncertainties. Within this paper, we aim to systematically examine, classify, and model all prevalent types of uncertainties of our primary industrial case study, and then propose a unified framework for managing each identified uncertainty type and associated components in a multi-agent setting within the case study: LLM-based echocardiographic software systems. Through this real-world application, we will discuss the practical challenges and demonstrate the feasibility of realizing our proposed framework, showing that a structured uncertainty lifecycle significantly enhances the transparency and calibration of diagnostic reasoning in high-stakes clinical environments. In the future, we aim to implement and generalize this framework to other safety-critical domains.

## References

- [1] Josh Achiam, Steven Adler, Sandhini Agarwal, Lama Ahmad, Ilge Akkaya, Florencia Leoni Aleman, Diogo Almeida, Janko Altenschmidt, Sam Altman, Shyamal Anadkat, et al. 2023. Gpt-4 technical report. arXiv preprint arXiv:2303.08774 (2023).
- [2] Torsten Bandyszak, Marian Daun, Bastian Tenbergen, Patrick Kuhs, Stefanie Wolf, and Thorsten Weyer. 2020. Orthogonal uncertainty modeling in the engineering of cyber-physical systems. IEEE Transactions on Automation Science and Engineering 17, 3 (2020), 1250--1265.
- [3] Manuel F Bertoa, Loli Burgue˜ no, Nathalie Moreno, and Antonio Vallecillo. 2020. Incorporating measurement uncertainty into OCL/UML primitive datatypes. Software and Systems Modeling 19, 5 (2020), 1163--1189.
- [4] Manuel F Bertoa, Nathalie Moreno, Gala Barquero, Loli Burgue˜ no, Javier Troya, and Antonio Vallecillo. 2018. Expressing measurement uncertainty in OCL/UML datatypes. In European Conference on Modelling Foundations and Applications . Springer, 46--62.

- [5] George EP Box. 1976. Science and statistics. J. Amer. Statist. Assoc. 71, 356 (1976), 791--799.
- [6] Matteo Camilli, Angelo Gargantini, Patrizia Scandurra, and Catia Trubiani. 2021. Uncertainty-aware exploration in model-based testing. In 2021 14th IEEE Conference on Software Testing, Verification and Validation (ICST) . IEEE, 71--81.
- [7] Ferhat Ozgur Catak, Tao Yue, and Shaukat Ali. 2021. Prediction surface uncertainty quantification in object detection models for autonomous driving. In 2021 IEEE international conference on artificial intelligence testing (AITest) . IEEE, 93--100.
- [8] Chieh-Ju Chao, Imon Banerjee, Reza Arsanjani, Chadi Ayoub, Andrew Tseng, Jean-Benoit Delbrouck, Garvan C Kane, Francisco Lopez-Jimenez, Zachi Attia, Jae K Oh, et al. 2025. Evaluating large language models in echocardiography reporting: opportunities and challenges. European Heart Journal-Digital Health 6, 3 (2025), 326--339.
- [9] Jinkun Chen, Sher Badshah, Xuemin Yu, and Sijia Han. 2025. Static Sandboxes Are Inadequate: Modeling Societal Complexity Requires Open-Ended Co-Evolution in LLM-Based Multi-Agent Simulations. arXiv preprint arXiv:2510.13982 (2025).
- [10] Armen Der Kiureghian and Ove Ditlevsen. 2009. Aleatory or epistemic? Does it matter? Structural safety 31, 2 (2009), 105--112.
- [11] Liping Han, Shaukat Ali, Tao Yue, Aitor Arrieta, and Maite Arratibel. 2023. Uncertainty-aware robustness assessment of industrial elevator systems. ACM Transactions on Software Engineering and Methodology 32, 4 (2023), 1--51.
- [12] Xinyi Hou, Yanjie Zhao, Yue Liu, Zhou Yang, et al. 2024. Harnessing the Power of Large Language Models in Software Engineering: A Survey. IEEE Transactions on Software Engineering (2024). https: //doi.org/10.1109/TSE.2024.3356789
- [13] Jean-Marc J´ ez´ equel and Antonio Vallecillo. 2023. Uncertainty-aware simulation of adaptive systems. ACM Transactions on Modeling and Computer Simulation 33, 3 (2023), 1--19.
- [14] Khimya Khetarpal, Matthew Riemer, Irina Rish, and Doina Precup. 2022. Towards continual reinforcement learning: A review and perspectives. Journal of Artificial Intelligence Research 75 (2022), 1401--1476.
- [15] Andreas Klinke. 2024. A Theory of Uncertainty: Perspectives in Philosophy, Social Sciences, and Risk Research . Routledge.
- [16] David A Lane and Robert R Maxfield. 2005. Ontological uncertainty and innovation. Journal of evolutionary economics 15, 1 (2005), 3--50.
- [17] Aixin Liu, Bei Feng, Bing Xue, Bingxuan Wang, Bochao Wu, Chengda Lu, Chenggang Zhao, Chengqi Deng, Chenyu Zhang, Chong Ruan, et al. 2024. Deepseek-v3 technical report. arXiv preprint arXiv:2412.19437 (2024).
- [18] Xiaoou Liu, Tiejin Chen, Longchao Da, Hua Wei, et al. 2025. Uncertainty Quantification and Confidence Calibration in Large Language Models: A Survey. arXiv preprint arXiv:2503.15850 (2025).
- [19] Shutian Luo, Huanle Xu, Kejiang Ye, Guoyao Xu, Liping Zhang, Guodong Yang, and Chengzhong Xu. 2022. The power of prediction: microservice auto scaling via workload learning. In Proceedings of the 13th Symposium on Cloud Computing . 355--369.
- [20] Johannes M¨ akelburg, Diego Perez-Palacin, Raffaela Mirandola, and Maribel Acosta. 2025. Surveying Uncertainty Representation: A Unified Model for Cyber-Physical Systems. arXiv preprint arXiv:2503.23892 (2025).
- [21] Potsawee Manakul, Adian Liusie, and Mark Gales. 2023. Selfcheckgpt: Zero-resource black-box hallucination detection for generative large language models. In Proceedings of the 2023 conference on empirical methods in natural language processing . 9004--9017.
- [22] Object Management Group. 2024. Precise Semantics for Uncertainty Modeling (PSUM), Version 1.0 . Formal Specification. Object Management Group. https://www.omg.org/spec/PSUM/1.0/ Available at: https://www.omg.org/spec/PSUM/ .
- [23] Seung Yeob Shin, Karim Chaouch, Shiva Nejati, Mehrdad Sabetzadeh, Lionel C Briand, and Frank Zimmer. 2021. Uncertainty-aware specification and analysis for hardware-in-the-loop testing of cyber-physical systems. Journal of Systems and Software 171 (2021), 110813.

- [24] Ola Shorinwa et al. 2025. A Survey on Uncertainty Quantification of Large Language Models: Taxonomy, Challenges, and Future Directions. Comput. Surveys 58, 3 (2025).
- [25] Arabella Simpkin and Richard Schwartzstein. 2016. Tolerating uncertainty-the next medical revolution? New England Journal of Medicine 375, 18 (2016).
- [26] Anique Tahir, Lu Cheng, and Huan Liu. 2023. Fairness through aleatoric uncertainty. In Proceedings of the 32nd ACM International Conference on Information and Knowledge Management . 2372--2381.
- [27] Javier Troya, Nathalie Moreno, Manuel F Bertoa, and Antonio Vallecillo. 2021. Uncertainty representation in software models: a survey. Software and Systems Modeling 20, 4 (2021), 1183--1213.
- [28] Warren E Walker, Poul Harremo¨ es, Jan Rotmans, Jeroen P Van Der Sluijs, Marjolein BA Van Asselt, Peter Janssen, and Martin P Krayer von Krauss. 2003. Defining uncertainty: a conceptual basis for uncertainty management in model-based decision support. Integrated assessment 4, 1 (2003), 5--17.
- [29] Tianyang Wang, Yunze Wang, Jun Zhou, Benji Peng, Xinyuan Song, Charles Zhang, Xintian Sun, Qian Niu, Junyu Liu, Silin Chen, et al. 2025. From aleatoric to epistemic: Exploring uncertainty quantification techniques in artificial intelligence. arXiv preprint arXiv:2501.03282 (2025).
- [30] Wenxuan Wang, Zizhan Ma, Zheng Wang, Chenghan Wu, Jiaming Ji, Wenting Chen, Xiang Li, and Yixuan Yuan. 2025. A survey of llm-based agents in medicine: How far are we from baymax? arXiv preprint arXiv:2502.11211 (2025).
- [31] Zhihua Wen, Zhizhao Liu, Zhiliang Tian, Shilong Pan, Zhen Huang, Dongsheng Li, and Minlie Huang. 2025. Scenario-independent Uncertainty Estimation for LLM-based Question Answering via Factor Analysis. In Proceedings of the ACM on Web Conference 2025 . 2378--2390.
- [32] Zhiqiu Xia, Jinxuan Xu, Yuqian Zhang, and Hang Liu. 2025. A Survey of Uncertainty Estimation Methods on Large Language Models. In Findings of the Association for Computational Linguistics: ACL 2025 . 21381--21396.
- [33] Qinghua Xu, Shaukat Ali, Tao Yue, and Maite Arratibel. 2022. Uncertainty-aware transfer learning to evolve digital twins for industrial elevators. In Proceedings of the 30th ACM Joint European Software Engineering Conference and Symposium on the Foundations of Software Engineering . 1257--1268.
- [34] Ruiyao Xu and Kaize Ding. 2025. Large Language Models for Anomaly and Out-of-Distribution Detection: A Survey. In Findings of the Association for Computational Linguistics: NAACL 2025 , Luis Chiruzzo, Alan Ritter, and Lu Wang (Eds.). Association for Computational Linguistics, Albuquerque, New Mexico, 5992--6012. https://doi.org/10.18653/v1/2025.findings-naacl.333
- [35] Huihui Zhang, Man Zhang, Tao Yue, Shaukat Ali, and Yan Li. 2020. Uncertainty-wise requirements prioritization with search. ACM Transactions on Software Engineering and Methodology (TOSEM) 30, 1 (2020), 1--54.
- [36] Man Zhang, Shaukat Ali, and Tao Yue. 2019. Uncertainty-wise test case generation and minimization for cyber-physical systems. Journal of Systems and Software 153 (2019), 1--21.
- [37] Man Zhang, Shaukat Ali, Tao Yue, and Roland Norgre. 2017. Uncertainty-wise evolution of test ready models. Information and Software Technology 87 (2017), 140--159.
- [38] Man Zhang, Shaukat Ali, Tao Yue, Roland Norgren, and Oscar Okariz. 2019. Uncertainty-wise cyber-physical system test modeling. Software &amp; Systems Modeling 18, 2 (2019), 1379--1418.
- [39] Man Zhang, Shaukat Ali, Tao Yue, Roland Norgren, and Oscar Okariz. 2019. Uncertainty-wise cyber-physical system test modeling. Software &amp; Systems Modeling 18, 2 (2019), 1379--1418.
- [40] Man Zhang, Bran Selic, Shaukat Ali, Tao Yue, Oscar Okariz, and Roland Norgren. 2016. Understanding uncertainty in cyber-physical systems: a conceptual model. In European conference on modelling foundations and applications . Springer, 247--264.
- [41] Man Zhang, Tao Yue, Shaukat Ali, Bran Selic, Oscar Okariz, Roland Norgre, and Karmele Intxausti. 2018. Specifying uncertainty in use case models. Journal of Systems and Software 144 (2018), 573--603.