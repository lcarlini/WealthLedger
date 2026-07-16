import { Injectable, signal } from '@angular/core';
import {
  AppConfig,
  CreateWebWorkerMLCEngine,
  MLCEngineInterface,
} from '@mlc-ai/web-llm';

/** Compact analytics payload — numbers must already be computed by the app/API. */
export interface AiInsightPayload {
  totalAmountBrl?: number;
  investmentCount: number;
  investments: Array<{
    name: string;
    amount: number;
    currency: string;
    rateLabel: string;
    projectedGain?: number;
    belowInflation?: boolean;
  }>;
  cardCategories: Array<{ category: string; totalAmount: number }>;
  checkingCategories: Array<{ category: string; totalAmount: number }>;
  projectedGainBrl?: number;
  projectionYears?: number;
  ipca?: number | null;
  selic?: number | null;
}

/** Local WebLLM model id — must match webllm-model-config.json and deploy/1-setup.ps1. */
export const WEBLLM_MODEL_ID = 'Qwen3-4B-q4f32_1-MLC';

/** Served from Angular assets (src/assets/ai → /assets/ai). */
const MODEL_CONFIG_URL = '/assets/ai/webllm-model-config.json';

/** Expected wasm for @mlc-ai/web-llm 0.2.x — used in clearer error messages. */
const EXPECTED_MODEL_LIB = 'Qwen3-4B-q4f32_1_cs1k-webgpu.wasm';

@Injectable({ providedIn: 'root' })
export class BrowserAiService {
  private engine: MLCEngineInterface | null = null;
  private initPromise: Promise<void> | null = null;

  readonly ready = signal(false);
  readonly initializing = signal(false);
  readonly initProgress = signal(0);
  readonly initStatus = signal('');

  /**
   * Lazy-loads WebLLM + local Qwen3-4B weights on first use.
   * Safe to call multiple times; caches the engine for the session.
   */
  async ensureReady(): Promise<void> {
    if (this.ready() && this.engine) {
      return;
    }
    if (this.initPromise) {
      return this.initPromise;
    }

    this.initPromise = this.initialize();
    try {
      await this.initPromise;
    } catch (err) {
      this.initPromise = null;
      throw err;
    }
  }

  async generateInsight(payload: AiInsightPayload): Promise<string> {
    await this.ensureReady();
    if (!this.engine) {
      throw new Error('Local AI engine is not initialized.');
    }

    const dataSection = this.formatPayload(payload);
    const systemPrompt =
      'You are a concise financial assistant. Use ONLY the numbers and facts provided in the data. ' +
      'Do not invent values, rates, or financial calculations. ' +
      'Respond in English in 3 to 5 short paragraphs: portfolio overview, comment on investments and rates, ' +
      'and 1 or 2 practical suggestions (e.g. diversification, category spending, emergency fund). ' +
      'Reply with plain text only — no HTML, XML, markdown, or thinking tags.';

    const completion = await this.engine.chat.completions.create({
      stream: false,
      messages: [
        { role: 'system', content: systemPrompt },
        {
          role: 'user',
          content: `Financial data (already calculated by the app; do not recalculate):\n${dataSection}`,
        },
      ],
      temperature: 0.2,
      max_tokens: 900,
      extra_body: { enable_thinking: false },
    });

    const content = completion.choices?.[0]?.message?.content;
    const raw = typeof content === 'string' ? content.trim() : '';
    const text = sanitizeInsightText(raw);
    if (!text) {
      throw new Error('The model returned an empty response.');
    }
    return text;
  }

  /** WebLLM cleanModelUrl() requires absolute URLs and appends /resolve/main/ to model paths. */
  private resolveAppConfigUrls(appConfig: AppConfig): AppConfig {
    const origin = window.location.origin;
    return {
      ...appConfig,
      // Default Cache API fails on 100MB+ shards; IndexedDB handles the ~2 GB local model.
      cacheBackend: appConfig.cacheBackend ?? 'indexeddb',
      model_list: (appConfig.model_list ?? []).map((record) => ({
        ...record,
        model: this.toAbsoluteAssetUrl(record.model, origin),
        model_lib: this.toAbsoluteAssetUrl(record.model_lib, origin),
      })),
    };
  }

  private toAbsoluteAssetUrl(path: string | undefined, origin: string): string {
    if (!path) {
      return '';
    }
    if (path.startsWith('http://') || path.startsWith('https://')) {
      return path;
    }
    return new URL(path, origin).href;
  }

  private async preflightModelAssets(modelBaseUrl: string): Promise<void> {
    const modelRoot = modelBaseUrl.endsWith('/') ? modelBaseUrl : `${modelBaseUrl}/`;
    const configUrl = `${modelRoot}resolve/main/mlc-chat-config.json`;
    const configResponse = await fetch(configUrl);
    if (!configResponse.ok) {
      throw new Error(
        `Model config not found at ${configUrl} (${configResponse.status}). ` +
          'Re-run deploy/1-setup.ps1.'
      );
    }

    const tensorCacheUrl = `${modelRoot}resolve/main/tensor-cache.json`;
    const tensorCacheResponse = await fetch(tensorCacheUrl);
    if (!tensorCacheResponse.ok) {
      throw new Error(
        `Model weight index not found at ${tensorCacheUrl} (${tensorCacheResponse.status}). ` +
          'Re-run deploy/1-setup.ps1.'
      );
    }

    const wasmUrl = `${window.location.origin}/assets/ai/models/lib/${EXPECTED_MODEL_LIB}`;
    const wasmResponse = await fetch(wasmUrl, { method: 'HEAD' });
    if (!wasmResponse.ok) {
      throw new Error(
        `WebGPU library not found at ${wasmUrl} (${wasmResponse.status}). ` +
          'Re-run deploy/1-setup.ps1.'
      );
    }
  }

  private async initialize(): Promise<void> {
    this.initializing.set(true);
    this.initProgress.set(0);
    this.initStatus.set('Loading model configuration…');

    try {
      if (!('gpu' in navigator)) {
        throw new Error(
          'WebGPU is not available in this browser. Use a recent Chrome or Edge for local AI insights.'
        );
      }

      const gpu = (navigator as Navigator & { gpu?: { requestAdapter(): Promise<unknown> } }).gpu;
      const adapter = gpu ? await gpu.requestAdapter() : null;
      if (!adapter) {
        throw new Error(
          'WebGPU adapter not found. Enable GPU acceleration and try Chrome or Edge.'
        );
      }

      const response = await fetch(MODEL_CONFIG_URL);
      if (!response.ok) {
        throw new Error(
          `Local model config not found at ${MODEL_CONFIG_URL}. ` +
            'Run deploy/1-setup.ps1 again.'
        );
      }

      const appConfig = this.resolveAppConfigUrls((await response.json()) as AppConfig);
      if (!appConfig.model_list?.length) {
        throw new Error('Invalid webllm-model-config.json: model_list is empty.');
      }

      const modelRecord = appConfig.model_list[0];
      if (!modelRecord.model_lib?.includes(EXPECTED_MODEL_LIB)) {
        throw new Error(
          `Outdated WebGPU library in config (${modelRecord.model_lib}). ` +
            `Re-run deploy/1-setup.ps1 to download ${EXPECTED_MODEL_LIB}.`
        );
      }

      await this.preflightModelAssets(modelRecord.model);

      this.initStatus.set('Initializing WebLLM…');

      const worker = new Worker(new URL('../workers/ai.worker', import.meta.url), {
        type: 'module',
      });

      worker.addEventListener('error', (event) => {
        console.error('WebLLM worker error', event.message, event);
      });

      this.engine = await CreateWebWorkerMLCEngine(worker, WEBLLM_MODEL_ID, {
        appConfig,
        initProgressCallback: (report) => {
          const pct = Math.round((report.progress ?? 0) * 100);
          this.initProgress.set(pct);
          this.initStatus.set(report.text || 'Loading model…');
        },
      });

      this.ready.set(true);
      this.initProgress.set(100);
      this.initStatus.set('Model ready');
    } finally {
      this.initializing.set(false);
    }
  }

  private formatPayload(payload: AiInsightPayload): string {
    const invLines =
      payload.investments.length > 0
        ? payload.investments
            .slice(0, 20)
            .map((i) => {
              const gain =
                i.projectedGain != null
                  ? `; ${payload.projectionYears ?? 3}y projected gain: ${i.projectedGain.toFixed(2)}`
                  : '';
              const warn = i.belowInflation ? '; below inflation' : '';
              return `- ${i.name}: ${i.amount.toFixed(2)} ${i.currency} (${i.rateLabel}${gain}${warn})`;
            })
            .join('\n')
        : 'No investments registered.';

    const card =
      payload.cardCategories.length > 0
        ? 'Card spending (top categories): ' +
          payload.cardCategories
            .slice(0, 10)
            .map((c) => `${c.category}: R$ ${c.totalAmount.toFixed(2)}`)
            .join('; ')
        : 'No card spending data.';

    const checking =
      payload.checkingCategories.length > 0
        ? 'Checking spending (top categories): ' +
          payload.checkingCategories
            .slice(0, 10)
            .map((c) => `${c.category}: R$ ${c.totalAmount.toFixed(2)}`)
            .join('; ')
        : 'No checking spending data.';

    const total =
      payload.totalAmountBrl != null
        ? `Total net worth (BRL): R$ ${payload.totalAmountBrl.toFixed(2)}`
        : 'Total net worth: unavailable';

    const projected =
      payload.projectedGainBrl != null
        ? `Projected ${payload.projectionYears ?? 3}-year gain (BRL investments): R$ ${payload.projectedGainBrl.toFixed(2)}`
        : '';

    const rates = [
      payload.ipca != null ? `IPCA: ${payload.ipca}% p.a.` : null,
      payload.selic != null ? `Selic: ${payload.selic}% p.a.` : null,
    ]
      .filter(Boolean)
      .join('; ');

    return [
      total,
      `Investment count: ${payload.investmentCount}`,
      projected,
      rates ? `Indicators: ${rates}` : null,
      '',
      'Investments:',
      invLines,
      '',
      card,
      checking,
    ]
      .filter((line) => line != null && line !== '')
      .join('\n');
  }
}

/** Remove Qwen thinking blocks and stray markup from model output. */
export function sanitizeInsightText(text: string): string {
  return text
    .replace(/<think(?:ing)?>[\s\S]*?<\/think(?:ing)?>/gi, '')
    .replace(/<think>[\s\S]*?<\/redacted_thinking>/gi, '')
    .replace(/<[^>\s]+(?:\s[^>]*)?>/g, '')
    .replace(/\n{3,}/g, '\n\n')
    .trim();
}

/** Extract a readable message from WebLLM / worker failures. */
export function extractAiErrorMessage(err: unknown): string {
  const raw = extractRawAiErrorMessage(err);
  if (/Failed to execute 'add' on 'Cache'|Cache\.add\(\)/i.test(raw)) {
    return (
      `${raw} — Browser Cache API cannot store large model shards (~2 GB total). ` +
      'This build uses IndexedDB instead; hard-refresh the page. If it persists, clear site data ' +
      '(DevTools → Application), free disk space, and disable "Clear cookies when you close all windows" in Chrome.'
    );
  }
  if (/ArtifactIndexedDBCache failed to fetch/i.test(raw)) {
    return (
      `${raw} — Check DevTools → Network for 404s under /assets/ai/, then re-run ` +
      'deploy/1-setup.ps1.'
    );
  }
  return raw;
}

function extractRawAiErrorMessage(err: unknown): string {
  if (err instanceof Error && err.message) {
    return err.message;
  }
  if (typeof err === 'string' && err.trim()) {
    return err.trim();
  }
  if (err && typeof err === 'object') {
    const obj = err as Record<string, unknown>;
    if (typeof obj['message'] === 'string' && obj['message'].trim()) {
      return obj['message'].trim();
    }
    if (typeof obj['error'] === 'string' && obj['error'].trim()) {
      return obj['error'].trim();
    }
  }
  return 'Failed to generate AI insights with local WebLLM.';
}
