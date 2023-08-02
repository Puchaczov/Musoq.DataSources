using Musoq.DataSources.Kubernetes.Deployments;
using Musoq.DataSources.Kubernetes.PodContainers;
using Musoq.DataSources.Kubernetes.Pods;
using Musoq.DataSources.Kubernetes.SecretData;
using Musoq.Plugins;
using Musoq.Plugins.Attributes;

namespace Musoq.DataSources.Kubernetes;

/// <summary>
/// Kubernetes helper methods.
/// </summary>
public class KubernetesLibrary : LibraryBase
{
    /// <summary>
    /// Decodes the secret.
    /// </summary>
    /// <param name="row">The row</param>
    /// <returns>Decoded string</returns>
    [BindableMethod]
    public string DecodeSecret([InjectSpecificSource(typeof(SecretDataEntity))] SecretDataEntity row)
    {
        var text = System.Text.Encoding.UTF8.GetString(row.Value);
        var base64EncodedBytes = System.Convert.FromBase64String(text);
        
        return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
    }
    
    /// <summary>
    /// Decodes the secret.
    /// </summary>
    /// <param name="row">The row</param>
    /// <param name="encoding">Encoding</param>
    /// <returns>Decoded secret</returns>
    [BindableMethod]
    public string DecodeSecret([InjectSpecificSource(typeof(SecretDataEntity))] SecretDataEntity row, string encoding)
    {
        var text = System.Text.Encoding.GetEncoding(encoding).GetString(row.Value);
        var base64EncodedBytes = System.Convert.FromBase64String(text);
        
        return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
    }
    
    /// <summary>
    /// Encodes the secret.
    /// </summary>
    /// <param name="text">The text</param>
    /// <returns>Encoded secret</returns>
    [BindableMethod]
    public string EncodeSecret(string text)
    {
        var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(text);
        return Convert.ToBase64String(plainTextBytes);
    }

    /// <summary>
    /// Encodes the secret.
    /// </summary>
    /// <param name="text">The text</param>
    /// <param name="encoding">Encoding</param>
    /// <returns>Encoded secret</returns>
    [BindableMethod]
    public string EncodeSecret(string text, string encoding)
    {
        var plainTextBytes = System.Text.Encoding.GetEncoding(encoding).GetBytes(text);
        return Convert.ToBase64String(plainTextBytes);
    }

    // /// <summary>
    // /// Gets the deployment label.
    // /// </summary>
    // /// <param name="row">The row</param>
    // /// <param name="key">The key</param>
    // /// <returns>Label</returns>
    // [BindableMethod]
    // public string GetLabel([InjectSource] DeploymentEntity row, string key)
    // {
    //     return row.RawObject.Metadata.Labels[key];
    // }
    //
    // /// <summary>
    // /// Gets the pod label.
    // /// </summary>
    // /// <param name="row">The row</param>
    // /// <param name="key">The key</param>
    // /// <returns>Label</returns>
    // [BindableMethod]
    // public string GetLabel([InjectSource] PodEntity row, string key)
    // {
    //     return row.RawObject.Metadata.Labels[key];
    // }
    //
    // /// <summary>
    // /// Gets the pod container label.
    // /// </summary>
    // /// <param name="row">The row</param>
    // /// <param name="key">The key</param>
    // /// <returns>Label</returns>
    // [BindableMethod]
    // public string GetLabel([InjectSource] PodContainerEntity row, string key)
    // {
    //     return row.RawObjectMetadata.Labels[key];
    // }

    /// <summary>
    /// Gets the pod container label.
    /// </summary>
    /// <param name="row">The row</param>
    /// <param name="key">The key</param>
    /// <returns>Label</returns>
    [BindableMethod]
    public string? GetLabelOrDefault([InjectSpecificSource(typeof(IWithObjectMetadata))] IWithObjectMetadata row, string key)
    {
        if (row.Metadata.Labels == null)
            return null;
        
        if (row.Metadata.Labels.TryGetValue(key, out var keyValue))
            return keyValue;
        
        return null;
    }
}