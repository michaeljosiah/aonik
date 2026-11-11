// Preparation only: no production resources, rights or spend are activated here.
// Import this qualified image into the production registry and verify its digest
// before using these parameters in an owner-approved deployment.
using '../../modules/publishing-verifier.bicep'

param location = 'uksouth'
param workloadName = 'aonik'
param environmentName = 'prod'
param poolName = 'aonikprodpublishinglarge'
param capacityProfile = 'large-v1'
param verifierImage = 'aonikprodacr.azurecr.io/aonik-arke-verifier-sessions@sha256:23069f32cd6c5cf6c07a983c74f8ac0698409fe97ace4e9f2096b3620938e13b'
param tags = { owner: 'platform', product: 'arke-worlds' }
