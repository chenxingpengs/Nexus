using Nexus.Services.WebSocket.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nexus.Services.WebSocket;

public class StateRecoveryManager
{
    private readonly SortedDictionary<int, RecoveryAction> _recoveryActions;
    private readonly List<string> _completedSteps;
    private readonly object _lock = new();
    private bool _isRecovering;
    private string _currentStep = string.Empty;

    public event EventHandler<RecoveryProgressEventArgs>? ProgressChanged;
    public event EventHandler<RecoveryCompletedEventArgs>? RecoveryCompleted;
    public event EventHandler<RecoveryErrorEventArgs>? RecoveryError;

    public StateRecoveryManager()
    {
        _recoveryActions = new SortedDictionary<int, RecoveryAction>();
        _completedSteps = new List<string>();
    }

    public void RegisterAction(string key, Func<Task> action, int priority = 0)
    {
        lock (_lock)
        {
            var recoveryAction = new RecoveryAction
            {
                Key = key,
                Action = action,
                Priority = priority,
                IsCompleted = false
            };

            var uniquePriority = priority;
            while (_recoveryActions.ContainsKey(uniquePriority))
            {
                uniquePriority--;
            }

            _recoveryActions[uniquePriority] = recoveryAction;
        }
    }

    public void UnregisterAction(string key)
    {
        lock (_lock)
        {
            var action = _recoveryActions.Values.FirstOrDefault(a => a.Key == key);
            if (action != null)
            {
                var priority = _recoveryActions.FirstOrDefault(x => x.Value.Key == key).Key;
                _recoveryActions.Remove(priority);
            }
        }
    }

    public async Task<RecoveryResult> ExecuteRecovery()
    {
        lock (_lock)
        {
            if (_isRecovering)
            {
                return new RecoveryResult { Success = false, Errors = new List<string> { "恢复流程正在进行中" } };
            }
            _isRecovering = true;
            _completedSteps.Clear();
        }

        var totalSteps = _recoveryActions.Count;
        var completedSteps = 0;
        var errors = new List<string>();

        try
        {
            foreach (var kvp in _recoveryActions.OrderByDescending(x => x.Key))
            {
                var action = kvp.Value;

                if (action.IsCompleted)
                {
                    completedSteps++;
                    continue;
                }

                _currentStep = action.Key;

                ProgressChanged?.Invoke(this, new RecoveryProgressEventArgs
                {
                    TotalSteps = totalSteps,
                    CompletedSteps = completedSteps,
                    CurrentStep = action.Key,
                    IsCompleted = false
                });

                try
                {
                    await action.Action();
                    action.IsCompleted = true;
                    _completedSteps.Add(action.Key);
                    completedSteps++;

                    ProgressChanged?.Invoke(this, new RecoveryProgressEventArgs
                    {
                        TotalSteps = totalSteps,
                        CompletedSteps = completedSteps,
                        CurrentStep = action.Key,
                        IsCompleted = false
                    });
                }
                catch (Exception ex)
                {
                    action.ErrorMessage = ex.Message;
                    errors.Add($"{action.Key}: {ex.Message}");

                    RecoveryError?.Invoke(this, new RecoveryErrorEventArgs
                    {
                        Step = action.Key,
                        Error = ex
                    });
                }
            }

            var result = new RecoveryResult
            {
                Success = errors.Count == 0,
                CompletedSteps = completedSteps,
                TotalSteps = totalSteps,
                Errors = errors
            };

            RecoveryCompleted?.Invoke(this, new RecoveryCompletedEventArgs
            {
                Success = result.Success,
                CompletedSteps = completedSteps,
                TotalSteps = totalSteps,
                Errors = errors
            });

            return result;
        }
        finally
        {
            lock (_lock)
            {
                _isRecovering = false;
                _currentStep = string.Empty;
            }
        }
    }

    public void ClearCompletedSteps()
    {
        lock (_lock)
        {
            _completedSteps.Clear();
            foreach (var action in _recoveryActions.Values)
            {
                action.IsCompleted = false;
                action.ErrorMessage = null;
            }
        }
    }

    public RecoveryProgress GetProgress()
    {
        lock (_lock)
        {
            return new RecoveryProgress
            {
                TotalSteps = _recoveryActions.Count,
                CompletedSteps = _completedSteps.Count,
                CurrentStep = _currentStep,
                IsCompleted = !_isRecovering && _completedSteps.Count == _recoveryActions.Count
            };
        }
    }

    public int RegisteredActionCount => _recoveryActions.Count;

    public int CompletedStepCount => _completedSteps.Count;
}

public class RecoveryAction
{
    public string Key { get; set; } = string.Empty;
    public Func<Task> Action { get; set; } = () => Task.CompletedTask;
    public int Priority { get; set; }
    public bool IsCompleted { get; set; }
    public string? ErrorMessage { get; set; }
}

public class RecoveryResult
{
    public bool Success { get; set; }
    public int CompletedSteps { get; set; }
    public int TotalSteps { get; set; }
    public List<string> Errors { get; set; } = new();
    public string? ErrorMessage => Errors.Count > 0 ? string.Join("; ", Errors) : null;
}

public class RecoveryProgressEventArgs : EventArgs
{
    public int TotalSteps { get; set; }
    public int CompletedSteps { get; set; }
    public string CurrentStep { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
}

public class RecoveryCompletedEventArgs : EventArgs
{
    public bool Success { get; set; }
    public int CompletedSteps { get; set; }
    public int TotalSteps { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class RecoveryErrorEventArgs : EventArgs
{
    public string Step { get; set; } = string.Empty;
    public Exception Error { get; set; } = new();
}
