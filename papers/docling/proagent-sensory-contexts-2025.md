## ProAgent: Harnessing On-Demand Sensory Contexts for Proactive LLM Agent Systems

Bufang Yang † , 1 , Lilin Xu † , 2 , Liekang Zeng 1 , Yunqi Guo 1 , Siyang Jiang 1 , Wenrui Lu 1 , Kaiwei Liu 1 , Hancheng Xiang 3 , Xiaofan Jiang 2 , Guoliang Xing 1 , Zhenyu Yan ‡ , 1

1 The Chinese University of Hong Kong, Hong Kong SAR,

2 Columbia University, United States, 3 Purdue University, United States

Figure 1: A user scenario of ProAgent. Reactive agents initiate assistance only upon explicit request, while ProAgent automatically uses rich sensory contexts to offer proactive, seamless, and unobtrusive assistance.

<!-- image -->

as users risk missing timely information during live interactions or attention-intensive tasks. In contrast, proactive agents are designed to autonomously perceive environments, anticipate user needs, and deliver timely assistance, thereby reducing both physical and cognitive workload [36]. Shifting from reactive agents, which remain largely passive, to proactive agent systems with the autonomy to perceive, reason, and unobtrusively assist holds great potential.

Although earlier personal assistants with notification features exhibit basic proactivity, they are confined to rulebased workflows without agent reasoning, e.g., fall or abnormal heart rate alerts [5, 25]. While recent work explores proactive LLM agents [36], it remains limited to perceiving enclosed desktop environments, offering assistance in computer-use scenarios such as coding. Some agents (e.g., Google's Magic Cue [1]) can provide pre-defined assistance using text in specific Apps and functions. In contrast, humans are continuously immersed in massive ' contexts ' that can be sensed through wearable and mobile devices. Harnessing these contexts to enhance the proactivity of LLM agents holds great potential. Moreover, this seamless, handsfree perception closely aligns with the objective of proactive assistants: reducing both human physical and cognitive workloads. Though recent works [52, 56] explore proactive agents with sensor data, they only focus on limited scenarios and ignore the system overhead in real-world deployments.

To bridge this research gap, we propose a proactive agent system that harnesses the rich contexts surrounding humans

## ABSTRACT

Large Language Model (LLM) agents are emerging to transform daily life. However, existing LLM agents primarily follow a reactive paradigm, relying on explicit user instructions to initiate services, which increases both physical and cognitive workload. In this paper, we propose ProAgent, the first end-to-end proactive agent system that harnesses massive sensory contexts and LLM reasoning to deliver proactive assistance. ProAgent first employs a proactive-oriented context extraction approach with on-demand tiered perception to continuously sense the environment and derive hierarchical contexts that incorporate both sensory and persona cues. ProAgent then adopts a context-aware proactive reasoner to map these contexts to user needs and tool calls, providing proactive assistance. We implement ProAgent on Augmented Reality (AR) glasses with an edge server and extensively evaluate it on a real-world testbed, a public dataset, and through a user study. Results show that ProAgent achieves up to 33.4% higher proactive prediction accuracy, 16.8% higher tool-calling F1 score, and notable improvements in user satisfaction over state-of-the-art baselines, marking a significant step toward proactive assistants. A video demonstration of ProAgent is available at https://youtu.be/pRXZuzvrcVs.

## 1 INTRODUCTION

Large Language Model (LLM) agents [29] have the potential to revolutionize human life due to their advanced reasoning and interaction with the physical world, enabling new forms of personal assistance such as mobile task automation [49], home sensor coordination [11], and healthcare support [42]. The global market for devices with agents is expected to approach USD 200 billion within the next decade [9].

Most existing LLM agent systems follow a reactive paradigm, relying on explicit user instructions to initiate their services [23, 45, 49, 61]. Users must manually invoke the agent, typically by taking out the smartphone and issuing instructions, which requires excessive and repetitive human effort. This reactive nature of existing agents not only increases physical workload, but also elevates cognitive burden,

† Equal Contribution. ‡ Corresponding author.

Table 1: Comparison of ProAgent with existing studies. N.A. means not applicable as it is not an agent.

<!-- image -->

| Methods                                                                                                         | Agent Paradigm                                            | LLM Reasoning   | Tool Calling   | Sensory Context   | On-Demand Perception   |
|-----------------------------------------------------------------------------------------------------------------|-----------------------------------------------------------|-----------------|----------------|-------------------|------------------------|
| Rule-based Assist [5] AutoDroid [49] AutoIoT [45] SocialMind [52] Proactive Agent [36] ContextLLM [41] ProAgent | N.A. Reactive Reactive Proactive Proactive N.A. Proactive | ✘ ✔ ✔ ✔ ✔ ✔ ✔   | ✘ ✔ ✔ ✘ ✘ ✘ ✔  | ✔ ✘ ✘ ✔ ✘ ✔ ✔     | ✔ ✘ ✘ ✘ ✘ ✘ ✔          |

to proactively deliver unobtrusive assistance. As shown in Fig. 1, unlike reactive LLM agents, which only initiate tasks upon receiving explicit user instructions [33, 45, 49], proactive LLM agents must continuously perceive their environment to anticipate user intentions and provide unobtrusive assistance. We summarize the unique challenges as follows. First , delivering timely and content-appropriate proactive assistance requires deriving intention-related cues from massive sensor data. However, while recent work has explored LLMs and visual LLMs (VLMs) [31] to interpret sensor readings for general purposes [17, 50, 59], extracting proactiveoriented cues from massive, multi-dimensional, and heterogeneous sensor data remains challenging. Second , unlike existing LLM agents that follow a reactive paradigm based on explicit text instructions [33, 49], a context-aware proactive agent must map sensor contexts to user intentions, including the timing of proactive actions and required tool calls, posing challenges for LLM reasoning, especially for models running on edge platforms. Third , to avoid missing service opportunities, proactive agents must continuously sense in-situ contexts surrounding humans and perform ongoing LLM reasoning, creating significant system overhead, especially on resource-constrained mobile devices.

In this study, we introduce ProAgent, the first end-toend proactive agent system that integrates multisensory perception and LLM reasoning to deliver proactive assistance. ProAgent first adopts an on-demand tiered perception strategy that coordinates always-on, low-cost sensors with on-demand, higher-cost sensors to continuously capture proactive-relevant cues. ProAgent then employs a proactiveoriented context extraction approach to derive hierarchical contexts from the massive sensor data, incorporating both sensory and persona cues. Finally, it adopts a context-aware proactive reasoner using VLMs, mapping hierarchical contexts to user needs, including the timing and content of assistance, and the calling of external tools. To further reduce user disturbance, ProAgent also employs a temporal constraint strategy to avoid redundant proactive assistance.

We implement ProAgent on a testbed with AR glasses, a smartphone, and two edge servers (NVIDIA Jetson Orin and a personal laptop), and comprehensively evaluate it on both a real-world testbed and a public dataset. We deploy ProAgent on six VLMs of varying sizes (2/3/7/8/13/32 billion parameters). Experimental results illustrate that ProAgent achieves up to 33.4% higher accuracy in proactive prediction, 16.8% higher F1 score in tool calling, and 1.79x lower memory usage over state-of-the-art baselines. The user study demonstrates that ProAgent improves average satisfaction across five dimensions of proactive services by 38.9%. We summarize the contributions of this work as follows.

- We introduce ProAgent, the first end-to-end proactive agent system that considers proactive assistance in multisensory perception and LLM reasoning.
- We develop a proactive-oriented context extraction approach with an on-demand tiered perception mechanism that continuously senses the environment on demand and derives proactive-relevant cues from massive sensor data.
- We develop a context-aware proactive reasoner that maps hierarchical contexts to predictions of user needs and tool calling, delivering proactive and unobtrusive assistance under temporal constraints to avoid overload.
- Weimplement ProAgent on AR glasses with an edge server and evaluate it on a real-world testbed, a public dataset, and through a user study, validating its effectiveness in delivering timely and unobtrusive proactive assistance.

## 2 RELATED WORKS

LLM Agents for Mobile Systems. Recent studies have explored leveraging LLMs for task automation in mobile systems, including autonomous mobile UI operations [24, 49, 61], embedded programming [14, 45, 57], and sensor system coordination [11, 15, 33]. Several studies employ screenshots and multimodal LLMs (MLLMs) for UI understanding, enabling autonomous operations such as navigating and using various apps based on the user's explicit instructions [47, 61]. Studies also employ LLM agents to interpret user queries and control smart-home sensors [23, 33]. However, these systems function as reactive LLM agents, relying on explicit user instructions and lacking the ability to proactively utilize sensory contexts to autonomously initiate assistance.

Proactive Agents. Recent studies have explored proactive LLM systems [36, 52, 56]. Proactive Agent [36] and SocialMind [52] offer users proactive assistance during computer usage and social communication, respectively. Other studies explore predicting teammates' actions in multi-agent systems [60] and designing re-asking mechanisms to reduce ambiguity [62]. Previous personal assistants with notification features exhibit proactivity [5, 25], such as health alerts [5], while these rule-based systems follow fixed and predefined workflows and specific sensing models to identify trigger

events. However, existing work either relies on limited observations, such as computer desktops, or adopts a re-asking strategy to gather additional information, failing to fully leverage the rich sensory contexts surrounding humans and external tools to provide proactive assistance unobtrusively. Recent works [52, 56] explored proactive agents using sensor data, while they are either limited to understanding the necessity of proactive suggestions in a social context or predicting assistance from sensory inputs. In contrast, ProAgent focuses on-demand tiered perception for continuous sensing and an efficient reasoning pipeline for proactive assistance.

Understanding Sensor Contexts via LLMs. Recent studies have explored diverse approaches to leveraging LLMs for understanding sensor data. Several studies [20, 50] prompt LLMs with domain expertise and raw sensor recordings as demonstrations to perform sensing tasks. Others employ LLMs to reason over predictions from specialized small models, enabling a comprehensive understanding of sensor data [39, 41, 51, 52, 55]. Studies also align large-scale text with sensor data in a unified embedding space, enabling natural language interaction [17, 59, 63]. However, existing studies primarily focus on interpreting sensor data, providing general-purpose descriptions. In contrast, ProAgent takes a step further by not only understanding the current sensory context, but actively anticipating users' potential needs.

In summary, existing studies primarily focus on developing reactive LLM agents for mobile automation or utilizing LLMs to understand diverse sensor signals. However, there remains a research gap in developing a context-aware agent system that can harness the massive sensory contexts surrounding humans to enhance the proactivity of LLM agents.

## 3 BACKGROUND AND MOTIVATION

## 3.1 Application Scenarios

ProAgent is designed to offer proactive services to users with wearable devices and smartphones across a wide range of daily scenarios, such as commuting, shopping, and work. In these scenarios, ProAgent first performs human-like perception and derives sensory contexts from multi-modal signals, such as egocentric video, audio, and motion data. It then reasons over these contexts to identify the user's needs, determine the appropriate timing and service content, which is delivered via smart glasses or audio cues to reduce both physical and cognitive workloads. For example, during a social conversation about travel plans, ProAgent can proactively prompt the user with weather updates or agenda conflicts to support in-situ decision-making. It can also provide relevant guidance in other scenarios, such as dietary health advice while eating, product comparisons and reviews while shopping, or transport information upon approaching a bus stop. ProAgent can also support applications such as assistive systems for visual impairments [54] and healthcare [22].

## 3.2 Motivation and Challenges

3.2.1 Limitations of Reactive Agents. Existing studies primarily focus on reactive LLM agents [24, 45, 49], which require explicit user instructions to initiate tasks. This passivity imposes both physical and cognitive workloads on users.

Physical Workload. Prior to the advent of LLM agents, users relied on repeated interactions with services such as agenda and transit apps, resulting in a substantial physical burden. Although LLM agents can automate many of these tasks, existing systems remain primarily reactive, requiring explicit user instructions to initiate assistance. Users must still manually invoke agents whenever assistance is needed, such as unlocking a phone and navigating an interface before issuing a request. These actions are especially cumbersome during ongoing interactions, such as conversations, walking, or meetings, where attention is already occupied.

Cognitive Workload. The reactive nature of these agents also raises cognitive demands on the user, as users may miss opportunities where agents or external tools could aid decision-making, particularly during tasks requiring sustained attention. In contrast, proactive agents anticipate user needs and provide timely assistance, such as reminders or relevant tips, reducing cognitive load and divided attention.

3.2.2 Shifting to Context-Aware Proactive Agents. The limitations of existing reactive agents highlight the need for a paradigm shift toward proactive agents that can anticipate user needs and deliver timely support.

Knob for Proactive Agents: Massive Sensory Contexts. Without explicit user instructions, environmental perception becomes the primary cue for determining whether to initiate services and what services to provide. While prior studies have explored predicting user needs from desktop [36] or mobile UI observations [1], we argue that proactive agents in open-world scenarios require richer and more diverse contexts, inferred from sensor data, to deliver seamless proactive assistance. First, the abundant and ubiquitous sensor data from mobile devices, such as smart glasses and smartphones, enable human-like perception, allowing for more accurate predictions of user needs. Second, their hands-free nature aligns closely with the fundamental goal of proactive agents: reducing both the physical and cognitive workload of users. Intention Understanding in Sensory Contexts. Without explicit user instructions, proactive agents must identify proactive-relevant cues from abundant sensor data. However, existing MLLMs and sensor LLMs mainly focus on understanding or captioning sensor recordings [17, 41]. In contrast, proactive agents must take a step further by actively anticipating user needs, including timing, tools, and content.

<!-- image -->

(a) An example of a case that exhibits over-proactivity and tool-calling failures in proactive services.

<!-- image -->

It would be helpful to have assistance in comparing different types of rice.

(b) An example of a case where proactive needs are missed. Figure 2: Examples of adapting LLMs to context-aware proactive agent tasks, leading to failures such as overproactivity, missed needs, and tool-calling errors.

We conduct experiments by adapting existing reactive LLMs and VLMs to context-aware proactive agent tasks on the public dataset CAB-Lite [56]. For LLM agents, we additionally employ a separate VLM for visual captioning. We employ prompts and in-context learning (ICL) [13] with ten examples to adapt them to proactive reasoning. We use Acc-P to measure the accuracy of user need prediction, and use F1 and Acc-Args to assess tool calling performance [56]. Fig. 3 illustrates that these adapted agents achieve limited performance in proactive predictions and tool calling. Fig. 2 further provides examples of their struggle to extract proactive cues and personal patterns from extensive sensor data, leading to both excessive proactivity and missed detections that limit practical usability. Additionally, existing reactive LLM agents are designed primarily for explicit user queries rather than mapping sensory contexts to intentions and tool calls. This knowledge gap often causes tool failures, severely degrading the quality of proactive assistance, as shown in Fig. 2.

3.2.3 Overhead of Proactive Reasoning. Since proactive agent systems must continuously perceive the environment to understand user intention, it is important yet challenging to handle the system overhead of computing and energy.

Rich Yet High-Cost Continuous Perception. We first evaluate the impact of egocentric video on proactive agent reasoning using real-world data annotated with proactive service needs. Specifically, we evaluate proactive need detection and video caption quality with varying video sampling rates. Specifically, we use recall to measure whether frames requiring proactive service are captured, and two metrics

<!-- image -->

Figure 3: Adapting existing LLMs and VLMs to the proactive agents.

<!-- image -->

Recall

Sampling Ratio

Figure 4: Existing adaptive perception methods in proactive agent systems.

<!-- image -->

(a) Recall and caption quality. (b) Impact of scene variation. Figure 5: Impact of egocentric video on proactive agent reasoning. X-axis denotes periodic sampling interval.

<!-- image -->

(i.e., BLEU [40] and ROUGE [30]) to compare caption similarity between the 1-second interval and other intervals. Fig. 5a shows that reducing the sampling rate significantly lowers both recall and caption quality, highlighting the importance of egocentric video in proactive agent reasoning. However, always-on high-cost vision perception poses challenges for resource-constrained mobile devices such as smart glasses. Opportunities of Low-Cost Modalities. Next, we analyze the impacts of vision sampling rates across scenarios where users remain still or move dynamically in changing scenes. Fig. 5b shows that low sampling rates have minimal impact when users are stationary but can miss critical events in dynamic environments. This highlights the potential of incorporating diverse low-cost sensor data, such as location and motion, to guide high-cost vision sampling, reducing system overhead while maintaining proactive service quality. Adaptive Perception. We further evaluate strategies for adaptive sampling of vision data. Specifically, we evaluate AdaSense [38] and SpeakerSense [35], which use always-on IMU and audio to trigger vision sampling when key events such as movement and conversation are detected. In addition to recall , we measure the sampling ratio relative to 10s periodic sampling to evaluate efficiency. We also present the performance of 20s periodic sampling (P-20s) and examine the vision-only input filtering strategy Reducto [28]. Fig. 4 shows that, although they can filter out unnecessary video data, they cannot capture the rich information in multimodal sensor data to provide proactive assistance correctly. Results highlight the challenges of reducing continuous perception and reasoning overhead in proactive agent systems.

In summary, existing reactive agents require users' physical and cognitive effort, highlighting the need for proactive agents that can anticipate user needs and deliver unobtrusive

Figure 6: System overview of ProAgent.

<!-- image -->

assistance. However, the unique nature of proactive agents operating without explicit user instructions poses the following challenges: extracting proactive cues from massive sensor data to accurately anticipate user needs, and the system overhead of continuous perception and reasoning.

## 4 SYSTEM DESIGN

## 4.1 System Overview

ProAgent is an end-to-end proactive agent system that harnesses sensory contexts to provide unobtrusive assistance. Fig. 6 illustrates the system overview of ProAgent. Specifically, ProAgent first employs an on-demand tiered perception strategy to continuously capture the user's surrounding context via multi-modal sensors (§ 4.4). It coordinates multiple always-on low-cost sensors with on-demand high-cost sensors, adaptively adjusting their sampling rates to ensure efficient perception while capturing proactive-related cues. Next, ProAgent uses a proactive-oriented context extraction approach that derives hierarchical contexts from the massive sensor data (§ 4.2). Finally, ProAgent employs a contextaware proactive reasoner using VLMs to integrate the derived sensory contexts and personas and determine whether proactive assistance is required (§ 4.3). When required, the reasoner identifies and calls the appropriate external tools to deliver assistance, while a temporal constraint strategy ensures the assistance remains unobtrusive.

## 4.2 Proactive-Oriented Context Extraction

Although existing studies have explored leveraging LLMs or VLMs to extract insights from sensor data [17, 41], they primarily focus on general sensor data understanding rather than anticipating user intents and determining how to assist users, resulting in inaccurate need predictions in proactive agent systems. To address these challenges, ProAgent adopts a proactive-oriented context extraction approach that maps massive sensor data into hierarchical contexts, enabling a comprehensive understanding of user intent.

4.2.1 Hierarchical Context Structuring. ProAgent first structures massive and heterogeneous sensor data into hierarchical contexts comprising both sensory and persona contexts.

Figure 7: Performance of proactive agent reasoning and the system overhead as the number of irrelevant personas increases. 'Qw' is QwenVL, and 'In' is InternVL.

<!-- image -->

Sensory Contexts. ProAgent perceives multi-modal sensor data, including egocentric video, audio, motion, and location, to capture user-surrounding contexts. Non-vision modalities are converted into text-based cues, generating location, motion, and audio contexts. Specifically, ProAgent uses GPS coordinates together with reverse geocoding to identify nearby points of interest such as bus stations and supermarkets, and obtain their proximity. They provide the location contexts. In addition, ProAgent uses the smartphone's IMU to determine the user's motion state, classifying into static or moving, and to generate motion contexts. ProAgent also captures audio to identify whether conversations occur [46], as such interactions are prone to necessitate proactive services, and transcribes the speech into dialogue as audio contexts.

Persona Contexts . Since individuals vary in their reliance on and need for proactive services, the optimal timing and content of assistance can differ even under the same sensory context. To address this, ProAgent considers the diversity in users' backgrounds and abilities. In particular, ProAgent allows users to input their personas in natural language. These persona contexts abstract the user's profile and are expressed as textual descriptions of the user's preferences and traits.

- 4.2.2 Context-Aware Persona Retrieval. An individual may possess multiple personas. However, integrating all personas into agent reasoning can incur substantial system overhead and even reduce the accuracy of predicting user needs. Fig. 7 shows that as the number of personas irrelevant to the current scenario increases, relevant personas are overwhelmed in the lengthy prompt, reducing proactive prediction accuracy and leading to higher system overhead.

Figure 8: Pipeline of context-aware persona retrieval. The right side shows examples of retrieved personas.

<!-- image -->

To address this, we develop a context-aware persona retrieval approach that adaptively integrates personas into proactive reasoning. Fig. 8 shows the workflow. ProAgent first groups user personas by scenario in the offline stage. At runtime, it extracts coarse-grained visual context and then uses it to retrieve the corresponding scenario-relevant personas.

To obtain visual contexts, a straightforward approach is to employ VLMs to generate descriptions and use them to select appropriate personas for proactive reasoning. However, this is computationally expensive and would introduce significant latency. To address this, we develop a coarse-grained context extraction approach to retrieve personas efficiently. Specifically, ProAgent constructs a scenario-object bank B , where each entry ( o 𝑖 , 𝑠 𝑖 ) consists of a set of detected objects o 𝑖 paired with a scene category 𝑠 𝑖 . At runtime, ProAgent employs a lightweight visual detection model to extract objects from the current image, which provides a coarse-grained visual context c to identify the appropriate scenario 𝑠 based on the scenario-object bank. To mitigate ambiguity from visually similar scenarios, we retrieve the top𝑘 most similar entries M from the object-scenario bank B based on semantic similarity between c and each entry's object set in the bank as M = TopK ( o 𝑖 ,𝑠 𝑖 ) ∈ B ( sim ( 𝜙 ( c ) , 𝜙 ( o 𝑖 ))) , where 𝜙 (·) denotes using a pretrained model to obtain semantic embeddings. The final scenario 𝑠 is predicted as the most frequent category among M . Next, ProAgent performs adaptive retrieval by utilizing the predicted scenario 𝑠 to retrieve personas from the corresponding scenario group, i.e., 𝑷 𝒔 = D 𝑝𝑒𝑟𝑠𝑜𝑛𝑎 [ 𝑠 ] , where 𝐷 𝑝𝑒𝑟𝑠𝑜𝑛𝑎 denotes the grouped persona database, and retrieval is performed via the scenario index. Only the personas 𝑷 𝒔 from the target scenario are incorporated into the reasoning process (see § 4.3). The scenario categories in this study follow those defined in the CAB-Lite dataset [56].

## 4.3 Context-Aware Proactive Reasoner

Unlike existing reactive LLM agents [34, 49] that require explicit user instructions, we develop a context-aware proactive reasoner that interprets sensor data while simultaneously inferring users' potential needs. We first introduce the runtime workflow of the reasoner, followed by its training strategies.

4.3.1 Proactive Agent Reasoning. As shown in Fig. 9, ProAgent employs a VLM-based reasoner that takes as input the

Figure 9: Illustration of the context-aware proactive reasoner in ProAgent. 'Enc.' represents 'Encoder'.

<!-- image -->

vision data and the hierarchical contexts from § 4.2, including both sensory and persona contexts. The reasoner generates structured outputs that include thoughts, proactive score, tool calls, and the final assistance delivered to the user.

Sensory Contexts Inputs. A straightforward approach to building a proactive agent is to first employ MLLMs for sensor data interpretation, followed by utilizing an LLM agent for reasoning and tool calling [56]. However, this twostage pipeline introduces significant system overhead as it requires running two large models for each inference. To address this, ProAgent employs a VLM that integrates visual context extraction and agent reasoning into a unified process, as illustrated in Fig. 9. Specifically, the raw vision data is sent to the visual encoder, and the remaining hierarchical contexts obtained in Sec. 4.2, including location, motion, audio contexts, and personas, are sent to the text encoder.

Context-Aware CoT Reasoning. The reasoner first generates an explicit description of the current vision inputs using Chain-of-Thoughts (CoT) [48], e.g., 'The user is in a store, looking at various headphones, possibly considering...' . Our experimental results demonstrate that this step allows the reasoner to better understand the current situation than directly mapping inputs to outputs via supervised learning, and improves proactive prediction performance.

Proactive Predictions. Next, the reasoner generates a proactive score based on the current contexts. This score indicates the predicted level of user need for proactive assistance, ranging from 1 for the lowest to 5 for the highest. The proactive assistance is initiated only when the prediction exceeds a threshold. Otherwise, the user remains undisturbed. Users can adjust this threshold to align with their preferred level of agent proactivity, e.g., individuals with visual impairment may lower it to receive assistance more frequently.

Tool Calling for Proactive Agent. Once ProAgent identifies that the user requires proactive assistance, it calls external tools [27, 56], such as agendas and weather, to assist the user. Note that we categorize these tools into retrievalbased and execution-based tools. ProAgent can directly use retrieval-based tools, (e.g., weather updates and bus schedules), while for execution-based tools (e.g., Uber and email),

the agent only reasons about their use and prompts the user for confirmation, rather than executing them directly. Finally, ProAgent integrates sensory and persona contexts, thoughts, and tool results to generate the final assistance. The following is a high-level view of the prompt's structure.

Task Instructions: Instruct the VLM on the task, including receiving sensory and persona contexts, generating a thought, deciding whether to initiate a proactive service, and calling external tools if needed.

Tool Set:

Tool functions, descriptions, and arguments.

Personas:

Retrieved user personas: &lt; persona-1 &gt; , &lt; persona-2 &gt; , . . .

Sensory Contexts:

Location, motion, and audio contexts. &lt; IMAGE &gt;

.

4.3.2 Context-Aware CoT Distillation. Although in-context learning (ICL) [13] can adapt MLLMs to proactive reasoning tasks, integrated examples incur additional inference overhead. Additionally, directly applying supervised fine-tuning (SFT) [6] on sensor data and proactive labels leads to limited understanding of the current situation and user intent.

To address this, we develop a context-aware CoT distillation approach to fine-tune the VLM. Specifically, we use the paired data &lt; img, sen, personas, thoughts, score, tools &gt; with SFT for fine-tuning, where img , sen , and persona are the inputs, indicating raw images, non-visual sensory contexts (location, motion, and audio), and personas, respectively. score and tools are the corresponding annotations for each sample, indicating the proactive score and tool calls. thoughts is the description of the current vision inputs, which are used to enable the VLM to learn to first generate explicit thoughts about the current context rather than directly generating proactive score and tool calls. We employ an additional advanced VLM with a proactive-oriented prompt to generate a description for each image, and it is used solely for training data generation. Details of the prompt see the Appendix. Finally, we train the VLM on the paired datasets and apply Low-Rank Adaptation (LoRA) [19] to reduce training cost.

4.3.3 Temporal Constraints on Proactivity. Since individuals often remain in similar environments for a while, such as waiting at a station or examining a product in a store, a proactive agent may repeatedly provide similar assistance, causing unnecessary disturbances. To address this, ProAgent employs a temporal constraint mechanism to avoid prompting users repeatedly within a period of time. Specifically, ProAgent calculates the semantic similarity between consecutive outputs using BERT [12] and delivers the output to the user only when the similarity falls below a threshold. Users can also adjust this threshold to align with personal preferences in temporal sensitivity to proactive assistance.

## 4.4 On-Demand Tiered Perception

Compared to reactive LLM agents, proactive agent systems must continuously perceive sensory contexts, posing additional challenges for mobile devices such as smart glasses, particularly in capturing high-cost vision data, as shown in

Figure 10: Tiered on-demand perception in ProAgent.

<!-- image -->

Fig. 5b. However, prior work on adaptive sampling strategies can only work on predefined tasks using heuristic approaches like fixed rules [35, 38], failing to support LLM reasoning over open-world contexts (as shown in Fig. 4).

To address these challenges, ProAgent employs an ondemand tiered perception strategy, as illustrated in Fig. 10. Specifically, it keeps multiple low-cost sensors always on while only activating high-cost sensors on demand, based on patterns from the low-cost sensors and the agent's internal reflections. This enables ProAgent to continuously capture proactive cues with minimal overhead.

4.4.1 On-Demand Context Perception. Our preliminary results in Fig. 5b illustrate that egocentric vision data is crucial for proactive agent systems, while costly to capture continuously on edge devices. Therefore, ProAgent employs an adaptive strategy for on-demand visual context perception.

Specifically, ProAgent first employs always-on low-cost sensors to continuously capture the environment cues and human states at minimal cost, using the location, motion, and audio contexts (§ 4.2.1). ProAgent then uses a dual-mode sampling strategy for visual perception, dynamically switching between low- and high-rate sampling. By default, ProAgent operates in a low-rate mode to reduce system overhead, while it switches to a high-rate mode when patterns detected by the low-cost sensors indicate the need for finer perception. These patterns include user motion, proximity to POIs, and active conversations. Our preliminary results in Fig. 5b show that movement requires more frequent perception to avoid missing proactive services. Therefore, ProAgent switches to a higher visual sampling rate when the user is moving, near POIs within a certain range, or engaged in conversation, since these moments either involve rapid environmental changes or present higher opportunities for proactive assistance. We set the high- and low-rate sampling intervals in ProAgent to 5 s and 60 s, respectively, based on our preliminary results in Fig. 5b. Sampling rate can also be adapted based on device cues (e.g., battery and resource usage).

4.4.2 Periodic Agent Reflection. Relying solely on alwayson patterns to determine visual sampling rates handles only limited scenarios and risks misinterpreting user intents or missing proactive needs. For example, movement occurs both when a user casually walks down the street without

needing assistance and when browsing in a store where timely assistance could benefit decision-making. Accurately determining the appropriate sampling rate thus requires reasoning over the user's surrounding contexts.

To address this, we extend ProAgent with a dedicated tool for adapting the visual sampling rate. Specifically, the tool converts the VLM reasoner's output (e.g., whether assistance is needed) into the next visual sampling interval. Since a high need for proactive assistance at the current moment often indicates a higher chance in subsequent periods, ProAgent switches the sampling rate to high mode when proactive assistance is currently predicted and lowered otherwise to conserve resources. This adjustment is produced naturally during its reasoning process in § 4.3, without incurring additional overhead. ProAgent then employs an adaptive sampling scheduler that coordinates low-cost cues with agent reflection to determine vision sampling mode. They can override each other. For instance, when low-cost cues suggest a low rate but the agent infers that a high rate is required, the system promptly switches to high-rate vision perception.

## 5 EVALUATION

## 5.1 System Implementation

5.1.1 Testbed Setup. We implement ProAgent on the RayNeo X3 Pro [4] smart glasses with a back-end server as our hardware testbed for real-world evaluation. The glasses are powered by Qualcomm's Snapdragon AR1 Gen-1 wearable platform and feature a binocular full-color micro-LED waveguide display, 4 GB LPDDR4X RAM, and 32 GB eMMC storage. The Android client, implemented in Kotlin and Java ( ≈ 1.2 K LOC), packages sensor data with contextual metadata (timestamp, request type) and renders results in the AR overlay.

On the server side, we implement the system on heterogeneous platforms for evaluation, including two edge servers (an NVIDIA Jetson Orin and a laptop with RTX 1660 Ti) and two high-performance servers (one with an NVIDIA 4090 GPU and another with 8 × NVIDIA RTX A6000 GPUs). Unless noted, all results are from the NVIDIA Jetson AGX Orin. LLM/VLM inference runs on the Ollama framework [3] via Python wrappers, and the smart glasses communicate with the server via WiFi and cellular networks over HTTPS.

5.1.2 Configuration. We fine-tune the VLMs using LoRA with a rank of 8, training for 10 epochs at a learning rate of 5 × 10 -4 . For coarse-grained context extraction, we employ YOLO-v5 [21], pretrained on the Objects365 dataset [44]. In addition, we set 𝑘 = 30, and use all-MiniLM-L6-v2 [43] for semantic similarity comparison. We use Google's Geocoding and Places APIs for reverse geocoding and nearby PoI identification. The advanced VLM used in context-aware CoT distillation is Qwen2.5-VL-32B [7]. We implement the agent with an API-based function calling framework [27], with a

Figure 11: Our real-world testbed. We implement ProAgent on AR glasses with a smartphone. Data collection uses both the glasses and a chest-mounted camera, with participants providing in-situ annotations via the app.

<!-- image -->

tool set from [56], containing 20 tools. In our experiments, the threshold for the proactive score and temporal constraint are set to 3 and 0.5, respectively.

## 5.2 Experimental Setup

5.2.1 Dataset. We comprehensively evaluate ProAgent using both public dataset and real-world testbed.

CAB-Lite Dataset . This public dataset [56] comprises 300 samples covering nine common daily-life scenarios, including shopping, travel, chitchat, work, health, outdoors, cooking, leisure, and others. Each sample in the dataset includes egocentric video, audio, user personas, and annotations for proactive scores and tool calls. The toolset in this dataset includes 20 frequently used tools, such as CityWeather and DateTime . However, since this dataset lacks long-term sensor recording and low-cost sensor data such as IMU and GPS, we only use it to evaluate the VLM reasoner in ProAgent.

Evaluation on Real-World Testbed . We also evaluate ProAgent using data collected from the real-world testbed.

We recruited 20 volunteers for real-world data collection (12 males, 8 females), with an average age of 24.3. Participants varied in personality traits and education. As shown in Fig. 11, each participant wore devices with egocentric cameras, such as smartglasses or a chest-mounted camera, and used a Google Pixel 7 smartphone to capture sensor data, including audio, IMU, and GPS. Ten participants wore the RayNeo X3 Pro, while the other ten used the Insta360 [2]. Participants recorded sensor data during daily routines across nine common scenarios defined in CAB-Lite, with each session lasting an average of 25.1 minutes. Participants were required to annotate the collected data, identifying moments requiring proactive services and specifying the intended tools. Participants used a mobile app, as shown in Fig. 11, to immediately record any moments when they believed proactive services might be needed, thereby preventing potential omissions. They could either specify the intended tools manually or select them from a candidate set provided in the app.

Each participant maintains an average of 20 personas. Each data collection session contains an average of 28 proactive assistance events, each labeled within a 5-second window, producing 6,025 samples. The agent's toolset included 20 tools defined in CAB-Lite, with general tools like GetDateTime used most and specialized ones like GetHealthData less often. The study was approved by the author institution's IRB, and all participants provided informed consent.

5.2.2 Evaluation Metrics. Weextensively evaluate ProAgent's performance from the following perspectives: trigger accuracy, content quality, and system overhead.

Trigger Accuracy . This dimension evaluates whether proactive services are initiated at appropriate moments. First, following prior studies [36, 56], we use Acc-P (Proactive Accuracy) and MD (Missed Detection) to evaluate the system's accuracy in identifying moments that require proactive services and its miss detection rate. To assess on-demand tiered perception in ProAgent, we use recall to assess whether vision sampling is triggered when needed, and use sampling ratio relative to 1s interval sampling to evaluate efficiency.

Content Quality . This aspect evaluates the relevance and usefulness of proactive responses. Since the response content is closely related to the agent's use of external tools, we assess whether ProAgent calls the user-intended tools for proactive assistance. Following prior works [8, 56], we adopt F1 -score to compare tool names between the predicted and ground-truth tool sets and Acc-Args (Arguments Accuracy) to determine whether the agent correctly parses the tools' arguments. Any errors in the tool name, API call format, or incorrect parameters are treated as incorrect for Acc-Args . System Overhead . We also evaluate the system overhead of ProAgent, including the inference latency, communication time, token consumption, and memory consumption.

5.2.3 Baselines. To the best of our knowledge, no prior work has developed proactive agent systems with long-term sensor perception. To comprehensively evaluate ProAgent, we implemented strong baselines by adapting them to this setting. Since a proactive agent system requires both user-need prediction and continuous perception, we adopted strong approaches for each when building baselines, including: 1) a proactive agent and 2) an efficient perception strategy.

ContextLLM-P . The original ContextLLM [41] is designed to transform sensor data to descriptive contexts. We adapt its system prompt to enable it for proactive reasoning, using its intrinsic knowledge to assist users proactively. Moreover, we equip it with periodic visual sampling strategies set at 10s intervals, and use a VLM to generate visual sensory context. ContextAgent-Ada . It extends ContextLLM with tool-calling capabilities, enabling it to leverage external tools for user assistance. Moreover, it uses AdaSense [38] to adaptively sample vision data according to IMU-derived motion states.

<!-- image -->

Maybe reschedule?

(a) User's trajectory in daily life. (b) Examples of ProAgent's proactive assistance along users' trajectory.

Figure 12: End-to-end evaluation of ProAgent.

ContextAgent-SFT . This baseline [56] extends ContextLLM with both tool-calling capabilities and SFT-based fine-tuning of the LLM agent to enhance its reasoning abilities. It adopts periodic sampling strategies at 10s for perceiving vision data.

The three baselines above use a two-stage pipeline, where a VLM first generates visual contexts, followed by an LLM agent for proactive reasoning. Besides, we use two singlestage baselines, which unify the two stages into a VLM agent. VLM-ICL-R . This approach utilizes a VLM with few-shot examples for proactive reasoning. Additionally, it employs Reducto [28] to eliminate redundant video frames.

VLM-CoT-R . This approach employs a VLM with CoT [48] for proactive reasoning. It also leverages Reducto to filter out redundant video frames.

Note that for all baselines that employ ICL or CoT strategies, we integrated ten demonstrations from CAB-Lite [56] into the VLM/LLM's system prompt for task adaptation.

5.2.4 Models. We deploy ProAgent on six VLMs of different scales, including Qwen2.5-VL-3B/7B/32B [7], InternVL2.52B/8B [10], and LLaVA-1.5 [32]. By default, experiments use Qwen2.5-VL-3B, while baselines are evaluated on models of the same size (Qwen2.5-3B or Qwen2.5-VL-3B) for fairness.

## 5.3 An End-to-End Evaluation

We evaluate the end-to-end performance of ProAgent in delivering proactive assistance during daily routines. Fig. 12a shows a user's real-world trajectory, where several moments are marked to display egocentric images and the corresponding outputs of ProAgent. Red points mark the moments where ProAgent delivers proactive assistance, while the blue points indicate decisions not to disturb the user. Results in Fig. 12b show that ProAgent can proactively provide valuable assistance to users in their daily routines. For example, it can provide real-time bus schedules when it detects that a bus is departing from a stop, and offer real-time suggestions during conversations to help users make informed decisions. Additionally, ProAgent remains silent when its reasoning indicates that assistance is unnecessary. We also measure the

Figure 13: Overall performance. ConLLM and ConAgent are ContextLLM and ContextAgent, respectively.

<!-- image -->

Figure 14: An example of ProAgent's reasoning process, showing perceived sensory contexts, thoughts, proactive predictions, tool calling, and final assistance.

<!-- image -->

end-to-end latency on Nvidia Jetson Orin, with ProAgent averaging 4.5s, showing its capability to operate in real-world scenarios and deliver continuous, timely proactive support.

## 5.4 Overall Performance

5.4.1 Quantitative Results. We first evaluate the overall performance of ProAgent on the real-world testbed. Fig.13 demonstrates that ProAgent consistently outperforms baselines, achieving up to 33.4% higher Acc-P , 16.8% higher F1 , and 15.5% higher Acc-Args , validating its effectiveness in triggering timely proactive services and delivering informative assistance. Since ContextLLM-P relies solely on intrinsic knowledge without tool calling, its F1 and Acc-Args remain minimal. We also assess the system overhead of ProAgent during continuous proactive assistance. Fig. 13 illustrates that ProAgent requires only 0.86x sampling ratio of the best baseline. Additionally, compared to two-stage baselines such as ContextAgent-Ada, ProAgent requires only 0.56x memory usage and 0.25x token usage, respectively, as it unifies vision context extraction and agent reasoning into a single stage. ProAgent also achieves an average inference time of 4.5s, validating its ability to provide timely assistance.

5.4.2 Qualitative Results. Fig. 14 shows examples of reasoning traces from ProAgent and outputs from baselines. Results show that ProAgent can explicitly reason over the current context and generate proactive predictions. When a proactive service is needed, it calls external tools such as time and location to deliver informative assistance that assists the user. In contrast, ContextLLM-P struggles to map sensory contexts

Figure 15: Performance of adaptive persona retrieval. Length denotes persona input length. Ada and Rand indicate adaptive and random retrieval.

<!-- image -->

Figure 16: Performance of coarse-grained context extraction for context-aware persona retrieval.

<!-- image -->

to proactive predictions and lacks tool-calling capabilities, leading to less useful content in proactive assistance.

## 5.5 Effectiveness of System Module

5.5.1 Effectiveness of Proactive-Oriented Context Extraction. Coarse-grained Context Extraction. We first evaluate the coarse-grained vision context extraction. We compare ProAgent with four baselines: (i) a rule-based approach that uses the same detection model as ours and predefined rules specifying which objects are indicative of a particular scene; and (ii) three VLM-based baselines, where a VLM is prompted to identify the scene. Specifically, we employ SmolVLM [37], Qwen2.5-VL-3B [7], and InternVL3-1B [64]. Fig.16 shows that ProAgent achieves 18.3% higher accuracy while using 6.0x less memory than Qwen2.5-VL-3B. Moreover, ProAgent achieves a latency of 118.4ms for coarse-grained vision context extraction, significantly lower than most baselines. Adaptive Persona Retrieval. Wethenevaluate the adaptive persona retrieval of ProAgent on CAB-Lite. We compare its performance with using all personas from CAB-Lite and with random subsets of the same size. Fig. 15 shows that adaptive retrieval achieves up to 13.9% higher Acc-P , 4.4% higher F1 , and 3.8% higher Acc-Args . It can also reduce input length by 6.5x, greatly lowering system overhead for agent reasoning.

5.5.2 Effectiveness of Context-Aware CoT Distillation. We evaluate the context-aware CoT distillation in ProAgent. Specifically, we assess the impact of removing thoughts during SFT on the VLM agent's performance (denoted as w/o C). Fig. 17 shows that ProAgent achieves 3.6% higher Acc-P , 6.0% higher F1 , and 10.1% higher Acc-Args . This is because prompting the VLM reasoner to first generate a description of the current vision data enables it to better interpret the situation and infer the user's intent, thereby improving performance.

<!-- image -->

Figure 17: Ablation study.

<!-- image -->

- (a) On-demand perception.

<!-- image -->

Figure 18: Scenario Impact.

<!-- image -->

- (b) Proactive prediction.

Figure 19: Ablation study. w/o L, w/o M, w/o A, and w/o R mean removing location, motion, audio, and agent reflection. w/o V means removing vision modality.

5.5.3 Effectiveness of On-Demand Tiered Perception. Weevaluate the performance of on-demand tiered perception. Specifically, we compare ProAgent with several strong baselines: (i) Periodic vision sampling at fixed intervals of 𝑥 seconds (denoted as P-x); (ii) Reducto [28]; (iii) AdaSense [38]; (iv) SpeakerSense [35]. Fig. 20 shows that ProAgent achieves the best trade-off between sampling ratio and recall , validating the effectiveness of on-demand tiered perception.

We further provide examples of different approaches performing on-demand visual perception and always-on sampling over time, along with their predicted proactive scores and ground truth. Fig. 22 shows that the timing of visual sampling initiated by ProAgent aligns better with the moments when the user requires proactive services and more accurately predicts higher proactive scores at those moments. Further, Fig. 21 shows the semantic similarity of agent outputs across time intervals, with outputs with higher similarity not presented to the user. This temporal constraint mechanism prevents ProAgent from repeatedly disturbing users with identical outputs in consecutive periods. We also observe that the data traces in Fig 22 show several false positives and false negatives. The reason is that although ProAgent already considers personas for personalized assistance, there are still corner cases such as user requests exceeding the tool set or VLM capabilities. However, ProAgent still achieves 83.3% Acc-P , demonstrating its effectiveness in the real world.

5.5.4 Impact of Base VLMs. Next, we evaluate the performance of ProAgent using different VLMs as the base agent. Fig. 23 shows that increasing the base VLM size leads to improved proactive reasoning performance. Notably, scaling up the model produces larger gains in Acc-Args and F1 compared to Acc-P . For example, scaling up from 7B to 32B leads to a 3.1% increase in F1 , while Acc-P increases by only 1.1%. This is because F1 and Acc-Args reflect agent planning, which

<!-- image -->

Figure 20: Performance of on-demand tiered perception in ProAgent.

Figure 21: Performance of proactivity under temporal constraints.

<!-- image -->

Figure 22: Examples of different methods performing on-demand (blue) and always-on (white) sampling over time, with predicted proactive scores and ground truth.

<!-- image -->

is inherently more complex than the discriminative task of proactive need prediction, especially for smaller VLMs.

- 5.5.5 Impact of Scenarios. Wealsoevaluate the cross-scenario generalizability of ProAgent. Specifically, we tested ProAgent on real-world data from commuting and shopping scenarios, while the VLM was trained on a CAB-Lite dataset with these scenarios removed. We then evaluate the VLM trained on partial data (denoted as partial ) and on the full dataset (denoted as full ). Fig. 18 shows that ProAgent still achieves 81.6% Acc-P , validating its generalization capability.
- 5.5.6 Impact of Contexts. This section presents an ablation study on the effect of different contexts in ProAgent.

Impact of Modalities. We first evaluate the impact of different sensor modalities on ProAgent. Fig. 19a shows the performance of on-demand tiered perception when motion, location, audio, or agent reflection is removed individually. Results indicate that removing any input reduces performance. LLM reasoning has the largest effect, with recall dropping by 33.7%, while audio has the smallest impact, with only a 6.7% decrease. Fig. 19b shows that removing vision or audio substantially degrades proactive prediction, with vision removal causing a 26.6% drop in Acc-P .

Impact of Personas. Wealso evaluate the effect of personas. Specifically, we assess the performance of ProAgent without personas (denoted as w/o P) on CAB-Lite. Fig. 17 illustrates that removing personas causes a significant performance

Figure 23: Comparison of different base VLMs used in ProAgent. QwVL is Qwen2.5-VL. InVL is InternVL.

<!-- image -->

drop for ProAgent with Qwen2.5-VL-7B, resulting in 20.2% and 17.0% decreases in Acc-P and F1 , respectively.

## 5.6 System Overhead

We evaluate ProAgent's overhead in submodule inference time and communication latency across multiple hardware platforms and outdoor scenarios. Fig. 24 shows that ProAgent achieves reasoning times of 0.52 s on the A6000 and 3.89 s on the Jetson Orin. Notably, the Time-To-First-Token (TTFT) on both Jetson Orin and laptop (RTX 1660 Ti GPU) is under 300ms, showing that ProAgent can deliver timely proactive assistance when deployed on edge servers. The personas occupy 86.1KB of storage. Additionally, coarse-grained context extraction (CE) and temporal constraints (TC) incur 118.5ms and 58.6ms on Jetson Orin, respectively. ProAgent achieves average communication latencies of 321.2ms, 434.1ms, and 726.5ms in office, store, and subway scenarios, validating real-world usability. ProAgent consumes 4.2% and 12.1% more battery per hour than idle on the Google Pixel 7 and RayNeo X3 Pro, respectively, indicating it does not impose significantly more power overhead than regular use.

## 5.7 User Study

We conducted a user study with 20 participants to evaluate whether ProAgent meets their expectations as a proactive assistant. Each participant rated the proactive assistance on a 0-5 scale across five dimensions, with the questionnaire administered once. These dimensions include: Timeliness , which asks participants whether assistance is delivered at appropriate moments. Relevance asks participants how well the assistance matches the user's current context. Usefulness asks the practical value of proactive support in assisting reallife tasks. Intrusiveness asks participants whether proactive assistance causes disturbance. Finally, Satisfaction represents the user's overall impression and acceptance of the system.

Fig. 25 shows that ProAgent significantly outperforms the baseline in all dimensions, especially the Timeliness and Relevance . The main reasons are its on-demand sampling strategy, which reduces missed cues compared with the baseline, and its ability to extract proactive-related cues for accurate triggering. Results also show that ProAgent provides notably higher Usefulness , since it accurately maps contexts to user-intended tool calls, delivering informative proactive

Figure 24: System latency.

<!-- image -->

Figure 25: User study.

assistance. Additionally, the intrusiveness score averaged 4 out of 5, showing broad acceptance but also sensitivity differences across users, highlighting opportunities to further align assistance with individual habits. We further analyzed user feedback to understand both what users found helpful and where gaps remained. Most participants valued the dining suggestions, noting they often struggle to decide what to eat, while some found calorie or health tips unnecessary, likely due to personal preferences not being fully specified at setup time. Most participants also appreciated online pricecomparison assistance, while a few felt it was only useful for high-value items such as headphones and unnecessary for everyday low-cost goods. Overall, results demonstrate the practicality of ProAgent as a proactive assistant system.

## 6 DISCUSSION

Understanding Proactive Behaviors. Although the results validate ProAgent's practicality in real-world use, the data traces in Fig. 22 reveal several false positives and negatives. They may arise from requests that exceed tool or model capabilities and from incomplete persona descriptions. We also observe that larger models (32B) could improve accuracy (Fig. 23). Incorporating user feedback and scaling the size of the VLM and the tool set can further mitigate these gaps. Scalability of ProAgent. ProAgent is implemented with an API-based solution for agent tool calling. Other techniques, such as enhancing reasoning abilities [58], Model Context Protocol (MCP) [18], and efficient inference [16, 26, 53] are orthogonal to this work and can be integrated into ProAgent to further improve system capabilities and efficiency. Privacy Concerns. ProAgent can be run on personal devices or edge servers like laptops without cloud access, ensuring user data remains local and preserving privacy. It can also use hardware-based cues, such as visible lights, flashes, or sound alerts, to notify others during environmental capture.

## 7 CONCLUSION

We introduce ProAgent, the first end-to-end proactive agent system that harnesses rich human-surrounding sensory contexts and LLM reasoning to deliver seamless and unobtrusive proactive assistance. Extensive evaluations show that ProAgent significantly improves the accuracy of proactive prediction and overall quality of assistance in the real world.

## REFERENCES

- [1] 2025. 4 ways Pixel's Magic Cue can help you save time. https://blog. google/products/pixel/google-pixel-magic-cue-ai-feature/.
- [2] 2025. Insta360 GO. https://www.insta360.com/hk/product/insta360go.
- [3] 2025. Ollama. https://ollama.com/.
- [4] 2025. RayNeo X3 Pro. https://rayneo.cn/x3pro.html.
- [5] 2025. Use Fall Detection with Apple Watch. https://support.apple.com/en-hk/108896.
- [6] Josh Achiam, Steven Adler, Sandhini Agarwal, Lama Ahmad, Ilge Akkaya, Florencia Leoni Aleman, Diogo Almeida, Janko Altenschmidt, Sam Altman, Shyamal Anadkat, et al. 2023. Gpt-4 technical report. arXiv preprint arXiv:2303.08774 (2023).
- [7] Shuai Bai, Keqin Chen, Xuejing Liu, Jialin Wang, Wenbin Ge, Sibo Song, Kai Dang, Peng Wang, Shijie Wang, Jun Tang, Humen Zhong, Yuanzhi Zhu, Mingkun Yang, Zhaohai Li, Jianqiang Wan, Pengfei Wang, Wei Ding, Zheren Fu, Yiheng Xu, Jiabo Ye, Xi Zhang, Tianbao Xie, Zesen Cheng, Hang Zhang, Zhibo Yang, Haiyang Xu, and Junyang Lin. 2025. Qwen2.5-VL Technical Report. arXiv preprint arXiv:2502.13923 (2025).
- [8] Kinjal Basu, Ibrahim Abdelaziz, Subhajit Chaudhury, Soham Dan, Maxwell Crouse, Asim Munawar, Vernon Austel, Sadhana Kumaravel, Vinod Muthusamy, Pavan Kapanipathi, et al. 2024. API-BLEND: A Comprehensive Corpora for Training and Benchmarking API LLMs. In Proceedings of the 62nd Annual Meeting of the Association for Computational Linguistics (Volume 1: Long Papers) . 12859-12870.
- [9] Peter Belcak, Greg Heinrich, Shizhe Diao, Yonggan Fu, Xin Dong, Saurav Muralidharan, Yingyan Celine Lin, and Pavlo Molchanov. 2025. Small Language Models are the Future of Agentic AI. arXiv preprint arXiv:2506.02153 (2025).
- [10] Zhe Chen, Jiannan Wu, Wenhai Wang, Weijie Su, Guo Chen, Sen Xing, Muyan Zhong, Qinglong Zhang, Xizhou Zhu, Lewei Lu, et al. 2024. Internvl: Scaling up vision foundation models and aligning for generic visual-linguistic tasks. In Proceedings of the IEEE/CVF Conference on Computer Vision and Pattern Recognition . 24185-24198.
- [11] Ye Cheng, Minghui Xu, Yue Zhang, Kun Li, Ruoxi Wang, and Lian Yang. 2024. AutoIoT: Automated IoT Platform Using Large Language Models. IEEE Internet of Things Journal (2024).
- [12] Jacob Devlin, Ming-Wei Chang, Kenton Lee, and Kristina Toutanova. 2019. Bert: Pre-training of deep bidirectional transformers for language understanding. In Proceedings of the 2019 conference of the North American chapter of the association for computational linguistics: human language technologies, volume 1 (long and short papers) . 4171-4186.
- [13] Qingxiu Dong, Lei Li, Damai Dai, Ce Zheng, Jingyuan Ma, Rui Li, Heming Xia, Jingjing Xu, Zhiyong Wu, Tianyu Liu, et al. 2022. A survey on in-context learning. arXiv preprint arXiv:2301.00234 (2022).
- [14] Zachary Englhardt, Richard Li, Dilini Nissanka, Zhihan Zhang, Girish Narayanswamy, Joseph Breda, Xin Liu, Shwetak Patel, and Vikram Iyer. 2024. Exploring and characterizing large language models for embedded system development and debugging. In Extended Abstracts of the CHI Conference on Human Factors in Computing Systems . 1-9.
- [15] Yi Gao, Kaijie Xiao, Fu Li, Weifeng Xu, Jiaming Huang, and Wei Dong. 2024. ChatIoT: Zero-code Generation of Trigger-action Based IoT Programs. Proceedings of the ACM on Interactive, Mobile, Wearable and Ubiquitous Technologies 8, 3 (2024), 1-29.
- [16] Albert Gu and Tri Dao. 2023. Mamba: Linear-time sequence modeling with selective state spaces. arXiv preprint arXiv:2312.00752 (2023).
- [17] Jiaming Han, Kaixiong Gong, Yiyuan Zhang, Jiaqi Wang, Kaipeng Zhang, Dahua Lin, Yu Qiao, Peng Gao, and Xiangyu Yue. 2024. Onellm: One framework to align all modalities with language. In Proceedings of the IEEE/CVF Conference on Computer Vision and Pattern Recognition . 26584-26595.
- [18] Xinyi Hou, Yanjie Zhao, Shenao Wang, and Haoyu Wang. 2025. Model context protocol (mcp): Landscape, security threats, and future research directions. arXiv preprint arXiv:2503.23278 (2025).
- [19] Edward J Hu, Yelong Shen, Phillip Wallis, Zeyuan Allen-Zhu, Yuanzhi Li, Shean Wang, Lu Wang, Weizhu Chen, et al. 2022. Lora: Low-rank adaptation of large language models. ICLR 1, 2 (2022), 3.
- [20] Sijie Ji, Xinzhe Zheng, and Chenshu Wu. 2024. Hargpt: Are llms zeroshot human activity recognizers?. In 2024 IEEE International Workshop on Foundation Models for Cyber-Physical Systems &amp; Internet of Things (FMSys) . IEEE, 38-43.
- [21] Glenn Jocher, Ayush Chaurasia, Alex Stoken, et al. 2022. ultralytics/yolov5: v7. 0-YOLOv5 SOTA realtime instance segmentation, November 2022. Retrieved February 3 (2022), 2023.
- [22] Yubin Kim, Xuhai Xu, Daniel McDuff, Cynthia Breazeal, and Hae Won Park. 2024. Health-LLM: Large Language Models for Health Prediction via Wearable Sensor Data. In Conference on Health, Inference, and Learning . PMLR, 522-539.
- [23] Evan King, Haoxiang Yu, Sangsu Lee, and Christine Julien. 2024. Sasha: creative goal-oriented reasoning in smart homes with large language models. Proceedings of the ACM on Interactive, Mobile, Wearable and Ubiquitous Technologies 8, 1 (2024), 1-38.
- [24] Sunjae Lee, Junyoung Choi, Jungjae Lee, Munim Hasan Wasi, Hojun Choi, Steve Ko, Sangeun Oh, and Insik Shin. 2024. Mobilegpt: Augmenting llm with human-like app memory for mobile task automation. In Proceedings of the 30th Annual International Conference on Mobile Computing and Networking . 1119-1133.
- [25] Ying Lei, Yancheng Cao, Will Wang, Yuanzhe Dong, Changchang Yin, Weidan Cao, Ping Zhang, Jingzhen Yang, Bingsheng Yao, Yifan Peng, et al. 2025. WatchGuardian: Enabling User-Defined Personalized Justin-Time Intervention on Smartwatch. arXiv preprint arXiv:2502.05783 (2025).
- [26] Yaniv Leviathan, Matan Kalman, and Yossi Matias. 2023. Fast inference from transformers via speculative decoding. In International Conference on Machine Learning . PMLR, 19274-19286.
- [27] Minghao Li, Yingxiu Zhao, Bowen Yu, Feifan Song, Hangyu Li, Haiyang Yu, Zhoujun Li, Fei Huang, and Yongbin Li. 2023. API-Bank: A Comprehensive Benchmark for Tool-Augmented LLMs. In Proceedings of the 2023 Conference on Empirical Methods in Natural Language Processing . 3102-3116.
- [28] Yuanqi Li, Arthi Padmanabhan, Pengzhan Zhao, Yufei Wang, Guoqing Harry Xu, and Ravi Netravali. 2020. Reducto: On-camera filtering for resource-efficient real-time video analytics. In Proceedings of the Annual conference of the ACM Special Interest Group on Data Communication on the applications, technologies, architectures, and protocols for computer communication . 359-376.
- [29] Yuanchun Li, Hao Wen, Weijun Wang, Xiangyu Li, Yizhen Yuan, Guohong Liu, Jiacheng Liu, Wenxing Xu, Xiang Wang, Yi Sun, et al. 2024. Personal llm agents: Insights and survey about the capability, efficiency and security. arXiv preprint arXiv:2401.05459 (2024).
- [30] Chin-Yew Lin. 2004. Rouge: A package for automatic evaluation of summaries. In Text summarization branches out . 74-81.
- [31] Ji Lin, Hongxu Yin, Wei Ping, Pavlo Molchanov, Mohammad Shoeybi, and Song Han. 2024. Vila: On pre-training for visual language models. In Proceedings of the IEEE/CVF conference on computer vision and pattern recognition . 26689-26699.
- [32] Haotian Liu, Chunyuan Li, Qingyang Wu, and Yong Jae Lee. 2023. Visual Instruction Tuning.
- [33] Kaiwei Liu, Bufang Yang, Lilin Xu, Yunqi Guo, Neiwen Ling, Zhihe Zhao, Guoliang Xing, Xian Shuai, Xiaozhe Ren, Xin Jiang, et al. 2024. Tasking Heterogeneous Sensor Systems with LLMs. In Proceedings of the 22nd ACM Conference on Embedded Networked Sensor Systems . 901-902.

- [34] Kaiwei Liu, Bufang Yang, Lilin Xu, Yunqi Guo, Guoliang Xing, Xian Shuai, Xiaozhe Ren, Xin Jiang, and Zhenyu Yan. 2025. TaskSense: A Translation-like Approach for Tasking Heterogeneous Sensor Systems with LLMs. In Proceedings of the 23rd ACM Conference on Embedded Networked Sensor Systems . 213-225.
- [35] Hong Lu, AJ Bernheim Brush, Bodhi Priyantha, Amy K Karlson, and Jie Liu. 2011. Speakersense: Energy efficient unobtrusive speaker identification on mobile phones. In Pervasive Computing: 9th International Conference, Pervasive 2011, San Francisco, USA, June 12-15, 2011. Proceedings 9 . Springer, 188-205.
- [36] Yaxi Lu, Shenzhi Yang, Cheng Qian, Guirong Chen, Qinyu Luo, Yesai Wu, Huadong Wang, Xin Cong, Zhong Zhang, Yankai Lin, et al. 2024. Proactive Agent: Shifting LLM Agents from Reactive Responses to Active Assistance. arXiv preprint arXiv:2410.12361 (2024).
- [37] Andres Marafioti, Merve Noyan, Miquel Farré, Elie Bakouch, and Pedro Cuenca. 2024. Smolvlm-small yet mighty vision language model.
- [38] Marina Neseem, Jon Nelson, and Sherief Reda. 2020. AdaSense: adaptive low-power sensing and activity recognition for wearable devices. In 2020 57th ACM/IEEE Design Automation Conference (DAC) . IEEE, 1-6.
- [39] Xiaomin Ouyang and Mani Srivastava. 2024. LLMSense: Harnessing LLMs for high-level reasoning over spatiotemporal sensor traces. In 2024 IEEE 3rd Workshop on Machine Learning on Edge in Sensor Systems (SenSys-ML) . IEEE, 9-14.
- [40] Kishore Papineni, Salim Roukos, Todd Ward, and Wei-Jing Zhu. 2002. Bleu: a method for automatic evaluation of machine translation. In Proceedings of the 40th annual meeting of the Association for Computational Linguistics . 311-318.
- [41] Kevin Post, Reo Kuchida, Mayowa Olapade, Zhigang Yin, Petteri Nurmi, and Huber Flores. 2025. ContextLLM: Meaningful Context Reasoning from Multi-Sensor and Multi-Device Data Using LLMs. In Proceedings of ACM HOTMOBILE'25 . Association for Computing Machinery (ACM).
- [42] Jianing Qiu, Kyle Lam, Guohao Li, Amish Acharya, Tien Yin Wong, Ara Darzi, Wu Yuan, and Eric J Topol. 2024. LLM-based agentic systems in medicine and healthcare. Nature Machine Intelligence 6, 12 (2024), 1418-1420.
- [43] Nils Reimers and Iryna Gurevych. 2019. Sentence-bert: Sentence embeddings using siamese bert-networks. arXiv preprint arXiv:1908.10084 (2019).
- [44] Shuai Shao, Zeming Li, Tianyuan Zhang, Chao Peng, Gang Yu, Xiangyu Zhang, Jing Li, and Jian Sun. 2019. Objects365: A large-scale, highquality dataset for object detection. In Proceedings of the IEEE/CVF international conference on computer vision . 8430-8439.
- [45] Leming Shen, Qiang Yang, Yuanqing Zheng, and Mo Li. 2025. AutoIOT: LLM-Driven Automated Natural Language Programming for AIoT Applications. arXiv preprint arXiv:2503.05346 (2025).
- [46] Silero Team. 2024. Silero VAD: pre-trained enterprise-grade Voice Activity Detector (VAD), Number Detector and Language Classifier. https://github.com/snakers4/silero-vad.
- [47] Junyang Wang, Haiyang Xu, Haitao Jia, Xi Zhang, Ming Yan, Weizhou Shen, Ji Zhang, Fei Huang, and Jitao Sang. 2024. Mobile-agent-v2: Mobile device operation assistant with effective navigation via multiagent collaboration. arXiv preprint arXiv:2406.01014 (2024).
- [48] Jason Wei, Xuezhi Wang, Dale Schuurmans, Maarten Bosma, Fei Xia, Ed Chi, Quoc V Le, Denny Zhou, et al. 2022. Chain-of-thought prompting elicits reasoning in large language models. Advances in neural information processing systems 35 (2022), 24824-24837.
- [49] Hao Wen, Yuanchun Li, Guohong Liu, Shanhui Zhao, Tao Yu, Toby Jia-Jun Li, Shiqi Jiang, Yunhao Liu, Yaqin Zhang, and Yunxin Liu. 2024. Autodroid: Llm-powered task automation in android. In Proceedings of the 30th Annual International Conference on Mobile Computing and

Networking . 543-557.

- [50] Huatao Xu, Liying Han, Qirui Yang, Mo Li, and Mani Srivastava. 2024. Penetrative ai: Making llms comprehend the physical world. In Proceedings of the 25th International Workshop on Mobile Computing Systems and Applications . 1-7.
- [51] Huatao Xu, Panron Tong, Mo Li, and Mani Srivastava. 2024. AutoLife: Automatic Life Journaling with Smartphones and LLMs. arXiv preprint arXiv:2412.15714 (2024).
- [52] Bufang Yang, Yunqi Guo, Lilin Xu, Zhenyu Yan, Hongkai Chen, Guoliang Xing, and Xiaofan Jiang. 2025. SocialMind: LLM-based Proactive AR Social Assistive System with Human-like Perception for In-situ Live Interactions. Proceedings of the ACM on Interactive, Mobile, Wearable and Ubiquitous Technologies 9, 1 (2025), 1-30.
- [53] Bufang Yang, Lixing He, Neiwen Ling, Zhenyu Yan, Guoliang Xing, Xian Shuai, Xiaozhe Ren, and Xin Jiang. 2023. Edgefm: Leveraging foundation model for open-set learning on the edge. In Proceedings of the 21st ACM Conference on Embedded Networked Sensor Systems . 111-124.
- [54] Bufang Yang, Lixing He, Kaiwei Liu, and Zhenyu Yan. 2024. Viassist: Adapting multi-modal large language models for users with visual impairments. In 2024 IEEE International Workshop on Foundation Models for Cyber-Physical Systems &amp; Internet of Things (FMSys) . IEEE, 32-37.
- [55] Bufang Yang, Siyang Jiang, Lilin Xu, Kaiwei Liu, Hai Li, Guoliang Xing, Hongkai Chen, Xiaofan Jiang, and Zhenyu Yan. 2024. Drhouse: An llm-empowered diagnostic reasoning system through harnessing outcomes from sensor data and expert knowledge. Proceedings of the ACM on Interactive, Mobile, Wearable and Ubiquitous Technologies 8, 4 (2024), 1-29.
- [56] Bufang Yang, Lilin Xu, Liekang Zeng, Kaiwei Liu, Siyang Jiang, Wenrui Lu, Hongkai Chen, Xiaofan Jiang, Guoliang Xing, and Zhenyu Yan. 2025. ContextAgent: Context-Aware Proactive LLM Agents with Openworld Sensory Perceptions. In The 39th Annual Conference on Neural Information Processing Systems . NeurIPS.
- [57] Huanqi Yang, Mingzhe Li, Mingda Han, Zhenjiang Li, and Weitao Xu. 2024. EmbedGenius: Towards Automated Software Development for Generic Embedded IoT Systems. arXiv preprint arXiv:2412.09058 (2024).
- [58] En Yu, Kangheng Lin, Liang Zhao, Jisheng Yin, Yana Wei, Yuang Peng, Haoran Wei, Jianjian Sun, Chunrui Han, Zheng Ge, et al. 2025. Perception-r1: Pioneering perception policy with reinforcement learning. arXiv preprint arXiv:2504.07954 (2025).
- [59] Xiaofan Yu, Lanxiang Hu, Benjamin Reichman, Dylan Chu, Rushil Chandrupatla, Xiyuan Zhang, Larry Heck, and Tajana Rosing. 2025. SensorChat: Answering Qualitative and Quantitative Questions during Long-Term Multimodal Sensor Interactions. arXiv preprint arXiv:2502.02883 (2025).
- [60] Ceyao Zhang, Kaijie Yang, Siyi Hu, Zihao Wang, Guanghe Li, Yihang Sun, Cheng Zhang, Zhaowei Zhang, Anji Liu, Song-Chun Zhu, et al. 2024. Proagent: building proactive cooperative agents with large language models. In Proceedings of the AAAI Conference on Artificial Intelligence , Vol. 38. 17591-17599.
- [61] Chi Zhang, Zhao Yang, Jiaxuan Liu, Yucheng Han, Xin Chen, Zebiao Huang, Bin Fu, and Gang Yu. 2023. Appagent: Multimodal agents as smartphone users. arXiv preprint arXiv:2312.13771 (2023).
- [62] Xuan Zhang, Yang Deng, Zifeng Ren, See-Kiong Ng, and Tat-Seng Chua. 2024. Ask-before-plan: Proactive language agents for real-world planning. arXiv preprint arXiv:2406.12639 (2024).
- [63] Yuwei Zhang, Tong Xia, Jing Han, Yu Wu, Georgios Rizos, Yang Liu, Mohammed Mosuily, J Ch, and Cecilia Mascolo. 2024. Towards open respiratory acoustic foundation models: Pretraining and benchmarking. Advances in Neural Information Processing Systems 37 (2024), 27024-27055.

- [64] Jinguo Zhu, Weiyun Wang, Zhe Chen, Zhaoyang Liu, Shenglong Ye, Lixin Gu, Hao Tian, Yuchen Duan, Weijie Su, Jie Shao, et al. 2025.

Internvl3: Exploring advanced training and test-time recipes for opensource multimodal models. arXiv preprint arXiv:2504.10479 (2025).