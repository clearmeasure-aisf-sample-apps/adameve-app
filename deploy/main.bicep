// What the game runs on in one environment, as the application's own code (the system's hosting "own"):
// one Azure Static Web App on the Free plan. deploy.ps1 applies this file as the deployment stack
// stack-adameve-<environment>-web in the tier's resource group, then uploads the site with the deployment token it
// reads at that moment. No repository is linked, so the properties stay empty.
//
// Free plan: 100 GB of bandwidth a month and 250 MB for a site, no SLA, and at most 10 Free sites in a subscription.
// The plan exists in a few regions (Central US is one); the files are served from the platform's edge everywhere.
targetScope = 'resourceGroup'

@description('The environment: tdd or prod.')
param environmentName string

@description('The region of the resource and its management endpoint.')
param location string = 'centralus'

var system = 'adameve'
var deployable = 'web'

resource site 'Microsoft.Web/staticSites@2024-04-01' = {
  name: 'swa-${system}-${environmentName}-${deployable}'
  location: location
  tags: {
    system: system
    environment: environmentName
    deployable: deployable
  }
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {}
}

output siteName string = site.name
output url string = 'https://${site.properties.defaultHostname}'
