import os
from mlagents.trainers.trainer_controller import TrainerController
from mlagents.envs.environment import UnityEnvironment
from mlagents.trainers.trainer_util import load_config

def train_normal_enemy():
    # Set up environment and training configuration
    env_path = os.path.join(os.path.dirname(__file__), 'Level/Training RL Scenes/Training Creep')
    config_path = os.path.join(os.path.dirname(__file__), 'NormalEnemyCC.yaml')
    
    # Load configuration
    config = load_config(config_path)
    
    # Initialize environment
    env = UnityEnvironment(
        file_name=env_path,
        worker_id=0,
        base_port=5005,
        no_graphics=True
    )
    
    # Initialize trainer controller
    trainer_controller = TrainerController(
        env=env,
        trainer_config=config,
        run_id="NormalEnemyTraining",
        save_freq=10000,
        meta_curriculum=None,
        load_model=False,
        train_model=True,
        keep_checkpoints=5
    )
    
    # Start training
    trainer_controller.start_training()

if __name__ == "__main__":
    train_normal_enemy()