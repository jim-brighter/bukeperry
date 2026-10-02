#!/usr/bin/env node
import * as cdk from 'aws-cdk-lib/core';
import { ValheimLLMStack } from '../lib/llm-stack';

const app = new cdk.App();

new ValheimLLMStack(app, 'ValheimLLM', {
  env: { account: process.env.CDK_DEFAULT_ACCOUNT, region: 'us-east-1' },
});
