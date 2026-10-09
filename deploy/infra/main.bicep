// What the game runs on in one environment, as the application's own code (the system's hosting "own"): one
// container app, ca-<system>-<environment>-web, in the Azure Container Apps express environment the system owns
// (cae-<system>, in the system's group rg-<system>-apps), running the image of one version from the system's
// registry. deploy.ps1 applies this file as the deployment stack stack-<system>-<environment>-web in the tier's
// resource group. The environment is the system's: this file names it and creates nothing in its group.
targetScope = 'resourceGroup'

@description('The system the game belongs to (adameve).')
param system string

@description('The environment: tdd or prod.')
param environmentName string

@description('The version to run: the tag of the image, which /_version then answers.')
param version string

@description('Login server of the system\'s registry, which holds <system>/web:<version>.')
param registryServer string

@description('Resource ID of the identity that pulls the image (id-<system>-<environment>-app, in this group); the system gave it AcrPull.')
param pullIdentityId string

@description('Resource ID of the system\'s Container Apps express environment.')
param managedEnvironmentId string

@description('The region of the express environment; the app runs where its environment is.')
param location string

@description('The port the container listens on (the image sets ASPNETCORE_HTTP_PORTS).')
param port int = 8080

@description('Replicas kept running. 0: the app stops when nobody asks it and starts with the next request.')
@minValue(0)
param minReplicas int = 0

@description('The most replicas the app may run.')
@minValue(1)
param maxReplicas int = 1

// Not the keys "environment" and "deployable": the system's own probe watches container apps that carry those.
var tags = {
  system: system
  application: 'web'
  stage: environmentName
}

resource app 'Microsoft.App/containerApps@2025-01-01' = {
  name: 'ca-${system}-${environmentName}-web'
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${pullIdentityId}': {}
    }
  }
  properties: {
    environmentId: managedEnvironmentId
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: port
        // An express environment has no HTTP/2.
        transport: 'http'
        allowInsecure: false
      }
      // An express environment keeps no registry on the app: it must come with every request that names the image.
      registries: [
        {
          server: registryServer
          identity: pullIdentityId
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'web'
          image: '${registryServer}/${system}/web:${version}'
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
        }
      ]
      scale: {
        minReplicas: minReplicas
        maxReplicas: maxReplicas
      }
    }
  }
}

output app string = app.name
output location string = app.location
output url string = 'https://${app.properties.configuration.ingress.fqdn}'
output image string = '${registryServer}/${system}/web:${version}'
