import numpy as np
import matplotlib.pyplot as plt
import pandas as pd
from typing import Dict, List, Tuple, Optional
from dataclasses import dataclass
from enum import Enum

class RewardType(Enum):
    STEP = "step"
    ACTION = "action"

@dataclass
class PolicyRule:
    id: int
    description: str
    reward_value: float
    reward_type: RewardType
    expected_frequency: float = 1.0  # Expected occurrences per episode

class PPOAnalysisFramework:
    """
    Framework untuk analisis PPO pada tahap Analisis dan Perancangan
    Fokus pada theoretical analysis tanpa memerlukan training data aktual
    """
    
    def __init__(self, gamma: float = 0.99, lambda_gae: float = 0.95, epsilon: float = 0.2):
        self.gamma = gamma
        self.lambda_gae = lambda_gae
        self.epsilon = epsilon
        self.policy_rules: List[PolicyRule] = []
        
    def add_policy_rule(self, rule: PolicyRule):
        """Tambahkan aturan policy untuk analisis"""
        self.policy_rules.append(rule)
        
    def theoretical_reward_impact_analysis(self) -> pd.DataFrame:
        """
        Analisis theoretical impact dari setiap reward rule
        Tanpa memerlukan training data aktual
        """
        results = []
        
        for rule in self.policy_rules:
            # Theoretical analysis berdasarkan magnitude dan frequency
            impact_score = abs(rule.reward_value) * rule.expected_frequency
            
            # Kategorisasi impact
            if impact_score >= 0.5:
                impact_category = "High"
            elif impact_score >= 0.1:
                impact_category = "Medium"
            else:
                impact_category = "Low"
                
            # Behavioral prediction
            if rule.reward_value > 0:
                behavioral_tendency = "Encourage"
            else:
                behavioral_tendency = "Discourage"
                
            results.append({
                'Policy_ID': rule.id,
                'Description': rule.description,
                'Reward_Value': rule.reward_value,
                'Type': rule.reward_type.value,
                'Expected_Frequency': rule.expected_frequency,
                'Impact_Score': impact_score,
                'Impact_Category': impact_category,
                'Behavioral_Tendency': behavioral_tendency
            })
            
        return pd.DataFrame(results)
    
    def reward_sensitivity_analysis(self, reward_variations: List[float] = None) -> Dict:
        """
        Analisis sensitivitas reward terhadap perubahan nilai
        """
        if reward_variations is None:
            reward_variations = [0.5, 0.75, 1.0, 1.25, 1.5, 2.0]
            
        sensitivity_results = {}
        
        for rule in self.policy_rules:
            rule_sensitivity = []
            for multiplier in reward_variations:
                modified_reward = rule.reward_value * multiplier
                theoretical_impact = abs(modified_reward) * rule.expected_frequency
                rule_sensitivity.append({
                    'multiplier': multiplier,
                    'modified_reward': modified_reward,
                    'theoretical_impact': theoretical_impact
                })
            
            sensitivity_results[f"Rule_{rule.id}"] = rule_sensitivity
            
        return sensitivity_results
    
    def hyperparameter_impact_analysis(self) -> Dict:
        """
        Analisis theoretical impact dari hyperparameter
        """
        # Gamma analysis - discount factor impact
        gamma_values = [0.9, 0.95, 0.99, 0.995]
        gamma_analysis = []
        
        for gamma in gamma_values:
            # Theoretical analysis: higher gamma = more future-focused
            future_weight = gamma ** 10  # Weight after 10 steps
            gamma_analysis.append({
                'gamma': gamma,
                'future_weight_10_steps': future_weight,
                'interpretation': 'High future focus' if gamma > 0.95 else 'Present focus'
            })
        
        # Epsilon analysis - clipping impact
        epsilon_values = [0.1, 0.2, 0.3]
        epsilon_analysis = []
        
        for eps in epsilon_values:
            epsilon_analysis.append({
                'epsilon': eps,
                'clip_range': f"[{1-eps:.1f}, {1+eps:.1f}]",
                'stability': 'High' if eps <= 0.2 else 'Medium' if eps <= 0.3 else 'Low'
            })
            
        return {
            'gamma_analysis': gamma_analysis,
            'epsilon_analysis': epsilon_analysis
        }
    
    def reward_balance_analysis(self) -> Dict:
        """
        Analisis keseimbangan antara positive dan negative rewards
        """
        positive_rewards = [r for r in self.policy_rules if r.reward_value > 0]
        negative_rewards = [r for r in self.policy_rules if r.reward_value < 0]
        
        total_positive = sum(r.reward_value * r.expected_frequency for r in positive_rewards)
        total_negative = sum(abs(r.reward_value) * r.expected_frequency for r in negative_rewards)
        
        balance_ratio = total_positive / total_negative if total_negative > 0 else float('inf')
        
        return {
            'positive_count': len(positive_rewards),
            'negative_count': len(negative_rewards),
            'total_positive_impact': total_positive,
            'total_negative_impact': total_negative,
            'balance_ratio': balance_ratio,
            'balance_assessment': self._assess_balance(balance_ratio)
        }
    
    def _assess_balance(self, ratio: float) -> str:
        """Assess reward balance quality"""
        if 0.8 <= ratio <= 1.2:
            return "Well Balanced"
        elif 0.5 <= ratio < 0.8 or 1.2 < ratio <= 2.0:
            return "Moderately Balanced"
        else:
            return "Imbalanced - Needs Adjustment"
    
    def generate_theoretical_lclip_bounds(self, sample_advantages: List[float]) -> Dict:
        """
        Generate theoretical L_CLIP bounds untuk different advantage values
        Useful untuk understanding clipping behavior
        """
        results = []
        
        for advantage in sample_advantages:
            # Theoretical ratio values
            ratios = np.linspace(0.5, 2.0, 100)
            
            unclipped_values = ratios * advantage
            clipped_ratios = np.clip(ratios, 1 - self.epsilon, 1 + self.epsilon)
            clipped_values = clipped_ratios * advantage
            
            final_values = np.minimum(unclipped_values, clipped_values)
            
            results.append({
                'advantage': advantage,
                'optimal_ratio_range': [1 - self.epsilon, 1 + self.epsilon],
                'max_unclipped': np.max(unclipped_values),
                'max_clipped': np.max(final_values),
                'clipping_active': np.max(unclipped_values) != np.max(final_values)
            })
            
        return results
    
    def visualize_reward_structure(self):
        """Visualisasi struktur reward untuk analisis"""
        df = self.theoretical_reward_impact_analysis()
        
        fig, ((ax1, ax2), (ax3, ax4)) = plt.subplots(2, 2, figsize=(15, 12))
        
        # 1. Reward values distribution
        colors = ['green' if x > 0 else 'red' for x in df['Reward_Value']]
        ax1.bar(range(len(df)), df['Reward_Value'], color=colors, alpha=0.7)
        ax1.set_title('Reward Values Distribution')
        ax1.set_xlabel('Policy Rules')
        ax1.set_ylabel('Reward Value')
        ax1.axhline(y=0, color='black', linestyle='-', alpha=0.3)
        
        # 2. Impact scores
        ax2.bar(range(len(df)), df['Impact_Score'], color='blue', alpha=0.7)
        ax2.set_title('Theoretical Impact Scores')
        ax2.set_xlabel('Policy Rules')
        ax2.set_ylabel('Impact Score')
        
        # 3. Reward type distribution
        type_counts = df['Type'].value_counts()
        ax3.pie(type_counts.values, labels=type_counts.index, autopct='%1.1f%%')
        ax3.set_title('Reward Type Distribution')
        
        # 4. Impact category distribution
        impact_counts = df['Impact_Category'].value_counts()
        ax4.pie(impact_counts.values, labels=impact_counts.index, autopct='%1.1f%%')
        ax4.set_title('Impact Category Distribution')
        
        plt.tight_layout()
        plt.show()
        
        return fig

# Implementasi untuk Normal Enemy dari dokumen
def create_normal_enemy_analysis():
    """
    Buat analisis untuk Normal Enemy berdasarkan dokumen
    """
    analyzer = PPOAnalysisFramework(gamma=0.99, lambda_gae=0.95, epsilon=0.2)
    
    # Definisi policy rules dari dokumen
    policy_rules = [
        PolicyRule(1, "Agent tidak bergerak sama sekali", -0.010, RewardType.STEP, 0.1),
        PolicyRule(2, "Agent mendekati tembok/obstacle", -0.8, RewardType.ACTION, 0.2),
        PolicyRule(3, "Agent bergerak dan berpatroli", 0.005, RewardType.STEP, 0.8),
        PolicyRule(4, "Agent menyelesaikan satu rotasi patroli", 0.5, RewardType.ACTION, 0.1),
        PolicyRule(5, "Agent gagal berpatroli", -0.01, RewardType.STEP, 0.3),
        PolicyRule(6, "Agent berhasil mendekati pemain", 0.01, RewardType.ACTION, 0.4),
        PolicyRule(7, "Agent gagal mendekati pemain", -0.05, RewardType.STEP, 0.2),
        PolicyRule(8, "Agent mendeteksi pemain saat patroli", 0.5, RewardType.ACTION, 0.3),
        PolicyRule(9, "Agent mengejar pemain", 0.010, RewardType.STEP, 0.5),
        PolicyRule(10, "Agent berhasil mengejar pemain", 0.9, RewardType.ACTION, 0.2),
        PolicyRule(11, "Agent tidak mengejar pemain", -0.05, RewardType.STEP, 0.1),
        PolicyRule(12, "Agent menyerang pemain", 0.8, RewardType.ACTION, 0.3),
        PolicyRule(13, "Agent tidak langsung menyerang", -0.01, RewardType.STEP, 0.2),
        PolicyRule(14, "Serangan agent tidak mengenai", -0.1, RewardType.ACTION, 0.4),
        PolicyRule(15, "Agent terkena serangan pemain", -0.7, RewardType.ACTION, 0.3),
        PolicyRule(16, "Agent menang melawan pemain", 1.0, RewardType.ACTION, 0.05),
        PolicyRule(17, "Agent kalah", -1.0, RewardType.ACTION, 0.1)
    ]
    
    for rule in policy_rules:
        analyzer.add_policy_rule(rule)
    
    return analyzer

# Contoh penggunaan
if __name__ == "__main__":
    # Buat analyzer untuk Normal Enemy
    analyzer = create_normal_enemy_analysis()
    
    # 1. Analisis impact theoretical
    print("=== THEORETICAL REWARD IMPACT ANALYSIS ===")
    impact_df = analyzer.theoretical_reward_impact_analysis()
    print(impact_df.to_string(index=False))
    
    print("\n=== REWARD BALANCE ANALYSIS ===")
    balance = analyzer.reward_balance_analysis()
    for key, value in balance.items():
        print(f"{key}: {value}")
    
    print("\n=== HYPERPARAMETER IMPACT ANALYSIS ===")
    hyper_analysis = analyzer.hyperparameter_impact_analysis()
    
    print("\nGamma Analysis:")
    for item in hyper_analysis['gamma_analysis']:
        print(f"Gamma {item['gamma']}: Future weight (10 steps) = {item['future_weight_10_steps']:.4f}")
    
    print("\nEpsilon Analysis:")
    for item in hyper_analysis['epsilon_analysis']:
        print(f"Epsilon {item['epsilon']}: Clip range = {item['clip_range']}, Stability = {item['stability']}")
    
    # 2. Sensitivity analysis
    print("\n=== REWARD SENSITIVITY ANALYSIS (Sample) ===")
    sensitivity = analyzer.reward_sensitivity_analysis()
    
    # Show sensitivity for first 3 rules
    for i, (rule_name, data) in enumerate(list(sensitivity.items())[:3]):
        print(f"\n{rule_name}:")
        for item in data:
            print(f"  Multiplier {item['multiplier']}: Modified reward = {item['modified_reward']:.3f}, Impact = {item['theoretical_impact']:.3f}")
    
    # 3. Theoretical L_CLIP bounds
    print("\n=== THEORETICAL L_CLIP BOUNDS ===")
    sample_advantages = [-2.0, -0.5, 0.5, 1.0, 2.0]
    lclip_bounds = analyzer.generate_theoretical_lclip_bounds(sample_advantages)
    
    for bound in lclip_bounds:
        print(f"Advantage {bound['advantage']}: Clipping Active = {bound['clipping_active']}")
    
    # 4. Visualisasi (uncomment jika ingin melihat plot)
    # analyzer.visualize_reward_structure()