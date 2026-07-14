/// <reference lib="webworker" />
import { WebWorkerMLCEngineHandler } from '@mlc-ai/web-llm';

/**
 * WebLLM engine runs in this worker so model load/generation does not block the UI.
 * Model weights are loaded from /assets/ai/ (local files, not Hugging Face at runtime).
 */
const handler = new WebWorkerMLCEngineHandler();

self.onmessage = (msg: MessageEvent): void => {
  handler.onmessage(msg);
};
